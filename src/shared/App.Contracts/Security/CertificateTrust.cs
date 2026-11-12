namespace App.Contracts.Security;

using System;
using System.Collections.Generic;
using System.Formats.Asn1;
using System.Linq;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;

/// <summary>Where a trusted CA certificate comes from (trust store <c>Source</c> column).</summary>
public static class TrustSources
{
    /// <summary>Heimdall's own self-signed root CA.</summary>
    public const string HeimdallSelfSigned = "heimdall-self-signed";
    /// <summary>Heimdall's CA, subordinate to (signed by) an AD / enterprise CA.</summary>
    public const string HeimdallSubordinate = "heimdall-subordinate";
    /// <summary>An Active Directory Certificate Services (enterprise) CA chain imported by an admin.</summary>
    public const string ActiveDirectory = "ad";
    /// <summary>Any other CA certificate imported by an admin.</summary>
    public const string Imported = "imported";

    public static readonly IReadOnlyList<string> All = new[] { HeimdallSelfSigned, HeimdallSubordinate, ActiveDirectory, Imported };

    public static bool IsHeimdall(string? source) =>
        string.Equals(source, HeimdallSelfSigned, StringComparison.Ordinal) ||
        string.Equals(source, HeimdallSubordinate, StringComparison.Ordinal);
}

/// <summary>
/// Identity claims of a certificate, read from its subjectAltName.
/// Conventions: users carry a UPN otherName (1.3.6.1.4.1.311.20.2.3) and/or an rfc822Name e-mail;
/// computers carry dNSName = hostname (or FQDN) and the URI <c>urn:heimdall:machine:&lt;machine identifier&gt;</c>.
/// </summary>
public sealed record CertificateIdentity(
    IReadOnlyList<string> DnsNames,
    IReadOnlyList<string> Uris,
    IReadOnlyList<string> Emails,
    IReadOnlyList<string> UserPrincipalNames)
{
    public const string MachineUriPrefix = "urn:heimdall:machine:";
    public const string UpnOid = "1.3.6.1.4.1.311.20.2.3";
    public const string SubjectAltNameOid = "2.5.29.17";

    /// <summary>The machine identifier from the <c>urn:heimdall:machine:</c> URI, if present.</summary>
    public string? MachineIdentifier =>
        Uris.FirstOrDefault(u => u.StartsWith(MachineUriPrefix, StringComparison.OrdinalIgnoreCase)) is { } uri
            ? Uri.UnescapeDataString(uri.Substring(MachineUriPrefix.Length))
            : null;

    /// <summary>E-mail and UPN candidates for user mapping (UPN first), de-duplicated, lower case.</summary>
    public IReadOnlyList<string> UserCandidates =>
        UserPrincipalNames.Concat(Emails).Select(v => v.Trim().ToLowerInvariant()).Where(v => v.Length > 0).Distinct().ToList();

    public static string MachineUri(string machineIdentifier) => MachineUriPrefix + Uri.EscapeDataString(machineIdentifier.Trim());

    /// <summary>Parses the subjectAltName extension (plus a subject E= e-mail as a fallback).</summary>
    public static CertificateIdentity FromCertificate(X509Certificate2 certificate)
    {
        var dns = new List<string>();
        var uris = new List<string>();
        var emails = new List<string>();
        var upns = new List<string>();

        foreach (var extension in certificate.Extensions)
        {
            if (extension.Oid?.Value != SubjectAltNameOid) continue;
            try
            {
                var names = new AsnReader(extension.RawData, AsnEncodingRules.DER).ReadSequence();
                while (names.HasData)
                {
                    var tag = names.PeekTag();
                    if (tag.TagClass != TagClass.ContextSpecific)
                    {
                        names.ReadEncodedValue();
                        continue;
                    }
                    switch (tag.TagValue)
                    {
                        case 0: // otherName ::= SEQUENCE { type-id OID, value [0] EXPLICIT ANY }
                            var other = names.ReadSequence(new Asn1Tag(TagClass.ContextSpecific, 0, isConstructed: true));
                            var typeId = other.ReadObjectIdentifier();
                            var value = other.ReadSequence(new Asn1Tag(TagClass.ContextSpecific, 0, isConstructed: true));
                            if (typeId == UpnOid && value.PeekTag().HasSameClassAndValue(new Asn1Tag(UniversalTagNumber.UTF8String)))
                                upns.Add(value.ReadCharacterString(UniversalTagNumber.UTF8String));
                            break;
                        case 1:
                            emails.Add(names.ReadCharacterString(UniversalTagNumber.IA5String, new Asn1Tag(TagClass.ContextSpecific, 1)));
                            break;
                        case 2:
                            dns.Add(names.ReadCharacterString(UniversalTagNumber.IA5String, new Asn1Tag(TagClass.ContextSpecific, 2)));
                            break;
                        case 6:
                            uris.Add(names.ReadCharacterString(UniversalTagNumber.IA5String, new Asn1Tag(TagClass.ContextSpecific, 6)));
                            break;
                        default:
                            names.ReadEncodedValue();
                            break;
                    }
                }
            }
            catch (AsnContentException)
            {
                // Malformed SAN: no identity from it.
            }
        }

        var subjectEmail = certificate.GetNameInfo(X509NameType.EmailName, forIssuer: false);
        if (!string.IsNullOrWhiteSpace(subjectEmail) && !emails.Contains(subjectEmail, StringComparer.OrdinalIgnoreCase))
            emails.Add(subjectEmail);

        return new CertificateIdentity(dns, uris, emails, upns);
    }
}

/// <summary>A CA certificate from the trust store with its source.</summary>
public sealed record TrustedCa(X509Certificate2 Certificate, string Source);

/// <summary>Outcome of <see cref="CertificateChainValidator.Validate"/>.</summary>
public sealed class ChainValidationResult
{
    public bool IsValid { get; init; }
    /// <summary>Valid, Expired, NotYetValid, UntrustedIssuer, InvalidSignature, Revoked, NotAnEndEntity, Invalid.</summary>
    public string Status { get; init; } = "Invalid";
    public string? Reason { get; init; }
    /// <summary>Leaf first, trust anchor last (empty when no chain was built).</summary>
    public IReadOnlyList<X509Certificate2> Chain { get; init; } = Array.Empty<X509Certificate2>();
    /// <summary>Source of the CA that directly issued the leaf (heimdall-*, ad, imported).</summary>
    public string? IssuerSource { get; init; }
    /// <summary>Source of the trust anchor the chain ends in.</summary>
    public string? AnchorSource { get; init; }
    public List<string> Warnings { get; init; } = new();

    public static ChainValidationResult Fail(string status, string reason, IReadOnlyList<X509Certificate2>? chain = null) =>
        new() { IsValid = false, Status = status, Reason = reason, Chain = chain ?? Array.Empty<X509Certificate2>() };
}

/// <summary>
/// X.509 chain validation against Heimdall's trust store, BCL only (X509Chain with a custom root
/// trust store, no machine or user store, no AIA downloads). Self-signed trusted CAs are anchors;
/// non-self-signed trusted CAs (e.g. a Heimdall CA subordinate to an AD root) are intermediates and
/// must chain to an anchor. Revocation: Heimdall-issued certificates are checked by the caller
/// against Heimdall's registry; for other CAs <paramref name="externalRevocationMode"/> enables a
/// best-effort CRL/OCSP check (unknown status is a warning, not a failure).
/// </summary>
public static class CertificateChainValidator
{
    public static ChainValidationResult Validate(
        X509Certificate2 leaf,
        IReadOnlyCollection<TrustedCa> trustedCas,
        IEnumerable<X509Certificate2>? extraIntermediates = null,
        DateTimeOffset? verificationTime = null,
        bool requireEndEntity = true,
        X509RevocationMode externalRevocationMode = X509RevocationMode.NoCheck)
    {
        if (trustedCas.Count == 0)
            return ChainValidationResult.Fail("UntrustedIssuer", "The trust store is empty.");

        if (requireEndEntity && IsCertificateAuthority(leaf))
            return ChainValidationResult.Fail("NotAnEndEntity", "A CA certificate cannot be used as a user or computer certificate.");

        var at = verificationTime ?? DateTimeOffset.UtcNow;
        var intermediates = (extraIntermediates ?? Enumerable.Empty<X509Certificate2>()).ToList();

        var (status, chain) = Build(leaf, trustedCas, intermediates, at, X509RevocationMode.NoCheck);
        if (status != null) return ChainValidationResult.Fail(status.Value.Status, status.Value.Reason, chain);

        var anchor = chain[^1];
        var anchorSource = SourceOf(anchor, trustedCas);
        if (anchorSource == null)
            return ChainValidationResult.Fail("UntrustedIssuer", "The chain does not end in a trusted CA.", chain);

        var issuerSource = chain.Count > 1 ? SourceOf(chain[1], trustedCas) ?? TrustSources.Imported : anchorSource;
        var result = new ChainValidationResult
        {
            IsValid = true,
            Status = "Valid",
            Chain = chain,
            IssuerSource = issuerSource,
            AnchorSource = anchorSource
        };

        if (!TrustSources.IsHeimdall(issuerSource))
        {
            if (externalRevocationMode == X509RevocationMode.NoCheck)
            {
                result.Warnings.Add("Revocation of certificates issued by external (AD / enterprise) CAs is not checked (Heimdall:Pki:ExternalRevocationMode=NoCheck).");
            }
            else
            {
                var (revStatus, _) = Build(leaf, trustedCas, intermediates, at, externalRevocationMode);
                if (revStatus is { Status: "Revoked" })
                    return ChainValidationResult.Fail("Revoked", revStatus.Value.Reason, chain);
                if (revStatus != null)
                    result.Warnings.Add($"Revocation status could not be confirmed: {revStatus.Value.Reason}");
            }
        }

        return result;
    }

    private static (( string Status, string Reason)? Failure, List<X509Certificate2> Chain) Build(
        X509Certificate2 leaf,
        IReadOnlyCollection<TrustedCa> trustedCas,
        List<X509Certificate2> intermediates,
        DateTimeOffset at,
        X509RevocationMode revocationMode)
    {
        using var chain = new X509Chain();
        chain.ChainPolicy.TrustMode = X509ChainTrustMode.CustomRootTrust;
        chain.ChainPolicy.DisableCertificateDownloads = true;
        chain.ChainPolicy.RevocationMode = revocationMode;
        chain.ChainPolicy.RevocationFlag = X509RevocationFlag.ExcludeRoot;
        chain.ChainPolicy.UrlRetrievalTimeout = TimeSpan.FromSeconds(5);
        chain.ChainPolicy.VerificationTime = at.UtcDateTime;
        foreach (var ca in trustedCas)
        {
            if (IsSelfIssued(ca.Certificate)) chain.ChainPolicy.CustomTrustStore.Add(ca.Certificate);
            else chain.ChainPolicy.ExtraStore.Add(ca.Certificate);
        }
        foreach (var extra in intermediates) chain.ChainPolicy.ExtraStore.Add(extra);

        bool ok = chain.Build(leaf);
        var elements = chain.ChainElements.Select(e => e.Certificate).ToList();
        if (ok) return (null, elements);

        var flags = chain.ChainStatus.Aggregate(X509ChainStatusFlags.NoError, (acc, s) => acc | s.Status);
        if (revocationMode != X509RevocationMode.NoCheck)
        {
            if (flags.HasFlag(X509ChainStatusFlags.Revoked))
                return (("Revoked", "The certificate (or a CA in its chain) has been revoked by its issuer."), elements);
            var revocationNoise = X509ChainStatusFlags.RevocationStatusUnknown | X509ChainStatusFlags.OfflineRevocation;
            if ((flags & ~revocationNoise) == X509ChainStatusFlags.NoError)
                return (("RevocationUnknown", "CRL / OCSP not reachable or not published."), elements);
        }

        if (flags.HasFlag(X509ChainStatusFlags.NotTimeValid))
        {
            var leafNotAfter = new DateTimeOffset(leaf.NotAfter.ToUniversalTime());
            var leafNotBefore = new DateTimeOffset(leaf.NotBefore.ToUniversalTime());
            if (at < leafNotBefore) return (("NotYetValid", $"The certificate is valid from {leafNotBefore:u}."), elements);
            if (at > leafNotAfter) return (("Expired", $"The certificate expired on {leafNotAfter:u}."), elements);
            return (("Expired", "A CA certificate in the chain is outside its validity period."), elements);
        }
        if (flags.HasFlag(X509ChainStatusFlags.NotSignatureValid))
            return (("InvalidSignature", "A signature in the chain does not verify."), elements);
        if (flags.HasFlag(X509ChainStatusFlags.UntrustedRoot) || flags.HasFlag(X509ChainStatusFlags.PartialChain))
            return (("UntrustedIssuer", "The certificate does not chain to a trusted CA."), elements);

        var detail = string.Join("; ", chain.ChainStatus.Select(s => $"{s.Status}: {s.StatusInformation.Trim()}"));
        return (("Invalid", string.IsNullOrWhiteSpace(detail) ? "Chain validation failed." : detail), elements);
    }

    private static string? SourceOf(X509Certificate2 certificate, IReadOnlyCollection<TrustedCa> trustedCas) =>
        trustedCas.FirstOrDefault(t => string.Equals(t.Certificate.Thumbprint, certificate.Thumbprint, StringComparison.OrdinalIgnoreCase))?.Source;

    public static bool IsSelfIssued(X509Certificate2 certificate) =>
        certificate.SubjectName.RawData.AsSpan().SequenceEqual(certificate.IssuerName.RawData);

    public static bool IsCertificateAuthority(X509Certificate2 certificate) =>
        certificate.Extensions.OfType<X509BasicConstraintsExtension>().Any(b => b.CertificateAuthority);

    /// <summary>All certificates of a PEM bundle (non-certificate blocks are ignored).</summary>
    public static List<X509Certificate2> ParsePemBundle(string? pem)
    {
        var result = new List<X509Certificate2>();
        if (string.IsNullOrWhiteSpace(pem)) return result;
        var collection = new X509Certificate2Collection();
        collection.ImportFromPem(pem);
        result.AddRange(collection.Cast<X509Certificate2>());
        return result;
    }

    /// <summary>SHA-256 fingerprint, hex upper case.</summary>
    public static string Sha256Fingerprint(X509Certificate2 certificate) =>
        Convert.ToHexString(SHA256.HashData(certificate.RawData));
}
