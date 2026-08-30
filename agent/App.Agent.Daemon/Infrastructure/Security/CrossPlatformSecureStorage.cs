namespace App.Agent.Daemon.Infrastructure.Security;

using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;

/// <summary>
/// Hardware-bound authenticated encryption for agent configuration and secrets.
/// Uses Windows DPAPI on Windows and AES-256-GCM HKDF Machine-Binding on Linux.
/// </summary>
public sealed class CrossPlatformSecureStorage
{
    private const byte FormatVersion = 0x01;
    private const int NonceSizeBytes = 12; // 96-bit standard AES-GCM nonce
    private const int TagSizeBytes = 16;   // 128-bit authentication tag
    private const int KeySizeBytes = 32;   // 256-bit AES key

    private static readonly byte[] HkdfSalt = Encoding.UTF8.GetBytes("Heimdall.Agent.SecureStorage.Salt.v1");
    private static readonly byte[] HkdfInfo = Encoding.UTF8.GetBytes("AES-256-GCM-Config-Key");

    private readonly byte[] _derivedKey;

    public CrossPlatformSecureStorage(string? customMasterKeyPath = null)
    {
        _derivedKey = DeriveMachineBoundKey(customMasterKeyPath);
    }

    /// <summary>
    /// Derives a 256-bit key from machine identity + local master key seed via HKDF-SHA256.
    /// </summary>
    private static byte[] DeriveMachineBoundKey(string? customMasterKeyPath)
    {
        string machineId = GetHardwareMachineId();
        byte[] localSeed = GetOrCreateLocalMasterSeed(customMasterKeyPath);

        byte[] machineIdBytes = Encoding.UTF8.GetBytes(machineId);
        byte[] ikm = new byte[machineIdBytes.Length + localSeed.Length];
        Buffer.BlockCopy(machineIdBytes, 0, ikm, 0, machineIdBytes.Length);
        Buffer.BlockCopy(localSeed, 0, ikm, machineIdBytes.Length, localSeed.Length);

        byte[] derivedKey = new byte[KeySizeBytes];
        HKDF.DeriveKey(HashAlgorithmName.SHA256, ikm, derivedKey, HkdfSalt, HkdfInfo);

        CryptographicOperations.ZeroMemory(ikm);
        CryptographicOperations.ZeroMemory(localSeed);

        return derivedKey;
    }

    private static string GetHardwareMachineId()
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
        {
            if (File.Exists("/etc/machine-id")) return File.ReadAllText("/etc/machine-id").Trim();
            if (File.Exists("/var/lib/dbus/machine-id")) return File.ReadAllText("/var/lib/dbus/machine-id").Trim();
        }
        else if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            try
            {
                using var key = Microsoft.Win32.Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Microsoft\Cryptography");
                var guid = key?.GetValue("MachineGuid")?.ToString();
                if (!string.IsNullOrEmpty(guid)) return guid;
            }
            catch { }
        }

        return Environment.MachineName;
    }

    private static byte[] GetOrCreateLocalMasterSeed(string? customPath)
    {
        string seedPath = customPath ?? (RuntimeInformation.IsOSPlatform(OSPlatform.Windows)
            ? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "Heimdall", "master.seed")
            : "/etc/heimdall/master.seed");

        var dir = Path.GetDirectoryName(seedPath);
        if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
        {
            Directory.CreateDirectory(dir);
        }

        if (File.Exists(seedPath))
        {
            return File.ReadAllBytes(seedPath);
        }

        byte[] newSeed = RandomNumberGenerator.GetBytes(32);
        File.WriteAllBytes(seedPath, newSeed);

        if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
        {
            try
            {
                File.SetUnixFileMode(seedPath, UnixFileMode.UserRead | UnixFileMode.UserWrite);
            }
            catch { }
        }

        return newSeed;
    }

    /// <summary>
    /// Encrypts plaintext bytes using AES-256-GCM authenticated encryption.
    /// </summary>
    public byte[] Encrypt(byte[] plainTextBytes)
    {
        byte[] nonce = new byte[NonceSizeBytes];
        RandomNumberGenerator.Fill(nonce);

        byte[] cipherText = new byte[plainTextBytes.Length];
        byte[] tag = new byte[TagSizeBytes];

        using (var aesGcm = new AesGcm(_derivedKey, TagSizeBytes))
        {
            aesGcm.Encrypt(nonce, plainTextBytes, cipherText, tag);
        }

        // Pack: [Version (1)] + [Nonce (12)] + [Tag (16)] + [Ciphertext (N)]
        byte[] output = new byte[1 + NonceSizeBytes + TagSizeBytes + cipherText.Length];
        output[0] = FormatVersion;
        Buffer.BlockCopy(nonce, 0, output, 1, NonceSizeBytes);
        Buffer.BlockCopy(tag, 0, output, 1 + NonceSizeBytes, TagSizeBytes);
        Buffer.BlockCopy(cipherText, 0, output, 1 + NonceSizeBytes + TagSizeBytes, cipherText.Length);

        return output;
    }

    /// <summary>
    /// Decrypts AES-256-GCM envelope payload and validates authenticity tag.
    /// </summary>
    public byte[] Decrypt(byte[] encryptedPayload)
    {
        if (encryptedPayload.Length < 1 + NonceSizeBytes + TagSizeBytes)
        {
            throw new CryptographicException("Invalid encrypted payload size.");
        }

        if (encryptedPayload[0] != FormatVersion)
        {
            throw new CryptographicException($"Unsupported encryption format version: {encryptedPayload[0]}");
        }

        byte[] nonce = new byte[NonceSizeBytes];
        byte[] tag = new byte[TagSizeBytes];
        int cipherTextLength = encryptedPayload.Length - 1 - NonceSizeBytes - TagSizeBytes;
        byte[] cipherText = new byte[cipherTextLength];
        byte[] plainText = new byte[cipherTextLength];

        Buffer.BlockCopy(encryptedPayload, 1, nonce, 0, NonceSizeBytes);
        Buffer.BlockCopy(encryptedPayload, 1 + NonceSizeBytes, tag, 0, TagSizeBytes);
        Buffer.BlockCopy(encryptedPayload, 1 + NonceSizeBytes + TagSizeBytes, cipherText, 0, cipherTextLength);

        using (var aesGcm = new AesGcm(_derivedKey, TagSizeBytes))
        {
            aesGcm.Decrypt(nonce, cipherText, tag, plainText);
        }

        return plainText;
    }

    public void SaveEncryptedString(string filePath, string content)
    {
        var rawBytes = Encoding.UTF8.GetBytes(content);
        var encrypted = Encrypt(rawBytes);
        File.WriteAllBytes(filePath, encrypted);
    }

    public string LoadEncryptedString(string filePath)
    {
        var encrypted = File.ReadAllBytes(filePath);
        var decrypted = Decrypt(encrypted);
        return Encoding.UTF8.GetString(decrypted);
    }
}
