namespace App.Shared.Plugins;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Text.Json;
using App.Contracts.Security;

/// <summary>
/// Detached JSON signature envelope (no CMS) for plugin packages.
/// User signature (<see cref="PluginSignatures.UserFormat"/>): the installing user signs SHA-256 of the
/// package file bytes with the key of their user certificate (AD- or Heimdall-issued).
/// Server countersignature (<see cref="PluginSignatures.ServerFormat"/>): the Heimdall server signs
/// SHA-256 of <c>"heimdall-plugin-countersignature/v1\n" + packageSha256 + "\n" + userSignature</c>
/// with its computer certificate.
/// </summary>
public record PluginSignatureEnvelope
{
    public string Format { get; init; } = PluginSignatures.UserFormat;
    /// <summary>RSA-PKCS1-SHA256, or ECDSA-SHA256 (DER signature as written by openssl; IEEE P1363 is also accepted).</summary>
    public string Algorithm { get; init; } = "RSA-PKCS1-SHA256";
    /// <summary>Hex SHA-256 of the package file bytes.</summary>
    public string PackageSha256 { get; init; } = string.Empty;
    /// <summary>Signer certificate first, then its intermediates (PEM). Trust anchors come from the verifier's trust store.</summary>
    public string CertificateChainPem { get; init; } = string.Empty;
    /// <summary>Base64 signature.</summary>
    public string Signature { get; init; } = string.Empty;
    public DateTimeOffset SignedAt { get; init; } = DateTimeOffset.UtcNow;
}

/// <summary>Package file format: <c>{ "manifest": {...}, "files": { "path": "content" } }</c> (UTF-8 JSON).</summary>
public record PluginPackageFile
{
    public PluginManifest Manifest { get; init; } = new();
    public Dictionary<string, string> Files { get; init; } = new();
}

/// <summary>Result of <see cref="PluginSignatures.VerifyDoubleSigned"/>.</summary>
public sealed record PluginSignatureCheck(bool IsValid, string? Reason, X509Certificate2? UserCertificate = null, X509Certificate2? ServerCertificate = null);

public static class PluginSignatures
{
    public const string UserFormat = "heimdall-plugin-signature/v1";
    public const string ServerFormat = "heimdall-plugin-countersignature/v1";
    public const string RsaPkcs1Sha256 = "RSA-PKCS1-SHA256";
    public const string EcdsaSha256 = "ECDSA-SHA256";
    /// <summary>Machine identifier in the server signing certificate (SAN URI urn:heimdall:machine:heimdall-server).</summary>
    public const string ServerMachineIdentifier = "heimdall-server";

    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

    public static string Sha256Hex(byte[] data) => Convert.ToHexString(SHA256.HashData(data)).ToLowerInvariant();

    public static byte[] CountersignedHash(string packageSha256, string userSignature) =>
        SHA256.HashData(Encoding.UTF8.GetBytes($"{ServerFormat}\n{packageSha256.ToLowerInvariant()}\n{userSignature}"));

    public static PluginPackageFile ParsePackage(byte[] packageBytes) =>
        JsonSerializer.Deserialize<PluginPackageFile>(packageBytes, JsonOptions) ?? throw new FormatException("Empty plugin package.");

    /// <summary>Signs <paramref name="hash"/> (SHA-256) with the certificate's private key.</summary>
    public static PluginSignatureEnvelope Sign(X509Certificate2 signer, string chainPem, byte[] hash, string packageSha256, string format)
    {
        string algorithm;
        byte[] signature;
        if (signer.GetRSAPrivateKey() is { } rsa)
        {
            using (rsa) signature = rsa.SignHash(hash, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
            algorithm = RsaPkcs1Sha256;
        }
        else if (signer.GetECDsaPrivateKey() is { } ecdsa)
        {
            using (ecdsa) signature = ecdsa.SignHash(hash, DSASignatureFormat.Rfc3279DerSequence);
            algorithm = EcdsaSha256;
        }
        else throw new InvalidOperationException("The signing certificate has no usable private key.");

        return new PluginSignatureEnvelope
        {
            Format = format,
            Algorithm = algorithm,
            PackageSha256 = packageSha256.ToLowerInvariant(),
            CertificateChainPem = chainPem,
            Signature = Convert.ToBase64String(signature),
            SignedAt = DateTimeOffset.UtcNow
        };
    }

    /// <summary>Verifies the envelope's signature over <paramref name="hash"/> with its signer certificate (no chain check).</summary>
    public static bool VerifySignature(PluginSignatureEnvelope envelope, byte[] hash, out X509Certificate2? signer)
    {
        signer = null;
        try
        {
            var chain = CertificateChainValidator.ParsePemBundle(envelope.CertificateChainPem);
            if (chain.Count == 0) return false;
            signer = chain[0];
            var signature = Convert.FromBase64String(envelope.Signature);
            if (string.Equals(envelope.Algorithm, RsaPkcs1Sha256, StringComparison.OrdinalIgnoreCase))
            {
                using var rsa = signer.GetRSAPublicKey();
                return rsa != null && rsa.VerifyHash(hash, signature, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
            }
            if (string.Equals(envelope.Algorithm, EcdsaSha256, StringComparison.OrdinalIgnoreCase))
            {
                using var ecdsa = signer.GetECDsaPublicKey();
                return ecdsa != null &&
                       (ecdsa.VerifyHash(hash, signature, DSASignatureFormat.Rfc3279DerSequence) ||
                        ecdsa.VerifyHash(hash, signature, DSASignatureFormat.IeeeP1363FixedFieldConcatenation));
            }
            return false;
        }
        catch (Exception ex) when (ex is FormatException or CryptographicException)
        {
            return false;
        }
    }

    /// <summary>
    /// Verifies a double-signed package: the user signature over SHA-256(package) and the server
    /// countersignature over (package hash + user signature), each by a certificate that chains to
    /// <paramref name="trustedCas"/>. Revocation of Heimdall-issued certificates is checked by the backend at upload.
    /// </summary>
    public static PluginSignatureCheck VerifyDoubleSigned(
        byte[] packageBytes,
        PluginSignatureEnvelope? userSignature,
        PluginSignatureEnvelope? serverSignature,
        IReadOnlyCollection<TrustedCa> trustedCas,
        DateTimeOffset? at = null,
        bool serverIssuerMustBeHeimdall = true)
    {
        if (userSignature == null || serverSignature == null)
            return new(false, "The plugin is not double-signed (user signature and server countersignature required).");
        if (userSignature.Format != UserFormat || serverSignature.Format != ServerFormat)
            return new(false, "Unknown signature envelope format.");

        var packageSha256 = Sha256Hex(packageBytes);
        if (!string.Equals(userSignature.PackageSha256, packageSha256, StringComparison.OrdinalIgnoreCase) ||
            !string.Equals(serverSignature.PackageSha256, packageSha256, StringComparison.OrdinalIgnoreCase))
            return new(false, "The package hash does not match the signed hash (tampered package).");

        if (!VerifySignature(userSignature, Convert.FromHexString(packageSha256), out var userCert) || userCert == null)
            return new(false, "The user signature does not verify.");
        var userChain = CertificateChainValidator.Validate(userCert, trustedCas,
            CertificateChainValidator.ParsePemBundle(userSignature.CertificateChainPem).Skip(1), at);
        if (!userChain.IsValid)
            return new(false, $"The user certificate is not trusted: {userChain.Status} - {userChain.Reason}", userCert);

        if (!VerifySignature(serverSignature, CountersignedHash(packageSha256, userSignature.Signature), out var serverCert) || serverCert == null)
            return new(false, "The server countersignature does not verify.", userCert);
        var serverChain = CertificateChainValidator.Validate(serverCert, trustedCas,
            CertificateChainValidator.ParsePemBundle(serverSignature.CertificateChainPem).Skip(1), at);
        if (!serverChain.IsValid)
            return new(false, $"The server certificate is not trusted: {serverChain.Status} - {serverChain.Reason}", userCert, serverCert);
        if (serverIssuerMustBeHeimdall && !TrustSources.IsHeimdall(serverChain.IssuerSource))
            return new(false, "The countersignature was not made with a Heimdall-issued server certificate.", userCert, serverCert);
        if (!string.Equals(CertificateIdentity.FromCertificate(serverCert).MachineIdentifier, ServerMachineIdentifier, StringComparison.OrdinalIgnoreCase))
            return new(false, $"The countersigning certificate is not the Heimdall server certificate (SAN URI {CertificateIdentity.MachineUriPrefix}{ServerMachineIdentifier}).", userCert, serverCert);
        if (serverCert.Thumbprint == userCert.Thumbprint)
            return new(false, "User signature and countersignature were made with the same certificate.", userCert, serverCert);

        return new(true, null, userCert, serverCert);
    }

    /// <summary>Deterministic hash of a file set (the manifest's PayloadHash): SHA-256 over "path:content;" sorted by path.</summary>
    public static string ComputePayloadHash(IReadOnlyDictionary<string, string> files)
    {
        var builder = new StringBuilder();
        foreach (var kvp in files.OrderBy(kv => kv.Key, StringComparer.Ordinal))
            builder.Append(kvp.Key).Append(':').Append(kvp.Value).Append(';');
        return Sha256Hex(Encoding.UTF8.GetBytes(builder.ToString()));
    }
}
