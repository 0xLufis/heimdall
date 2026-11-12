namespace App.Agent.Daemon.Infrastructure.Security;

using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using App.Agent.Daemon.Infrastructure.Persistence;

/// <summary>
/// Hardware-bound authenticated encryption for agent configuration, spool payloads, and secrets.
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

    /// <summary>How the master seed was obtained by this instance.</summary>
    public enum SeedOrigin
    {
        /// <summary>An existing, valid seed file was read.</summary>
        Loaded,
        /// <summary>No seed existed; a new one was generated (first use on this machine).</summary>
        Created,
        /// <summary>The seed file was missing or damaged and its backup copy was restored.</summary>
        RestoredFromBackup,
        /// <summary>
        /// The seed file and its backup were both unusable. A new seed was generated, which means
        /// everything encrypted with the previous one can no longer be decrypted.
        /// </summary>
        RegeneratedAfterCorruption
    }

    /// <summary>How the master seed was obtained (loaded, created, restored from backup, regenerated).</summary>
    public SeedOrigin MasterSeedOrigin { get; }

    /// <param name="customMasterKeyPath">Seed file location; the platform default when null.</param>
    /// <param name="recoveryJournal">Receives a notice when the seed had to be restored or regenerated.</param>
    public CrossPlatformSecureStorage(string? customMasterKeyPath = null, IRecoveryJournal? recoveryJournal = null)
    {
        _derivedKey = DeriveMachineBoundKey(customMasterKeyPath, out var origin, out var seedPath, out var detail);
        MasterSeedOrigin = origin;

        if (origin == SeedOrigin.RestoredFromBackup)
        {
            recoveryJournal?.Record(new RecoveryNotice
            {
                Component = "secure_storage",
                File = seedPath,
                Defect = detail ?? "invalid",
                RecoveredFrom = "backup",
                Severity = "Warning",
                Detail = "master seed restored from its backup copy; encrypted data remains readable"
            });
        }
        else if (origin == SeedOrigin.RegeneratedAfterCorruption)
        {
            recoveryJournal?.Record(new RecoveryNotice
            {
                Component = "secure_storage",
                File = seedPath,
                Defect = detail ?? "invalid",
                RecoveredFrom = "regenerated",
                Severity = "Critical",
                Detail = "master seed and its backup were unusable; a new seed was generated and data encrypted with the previous seed cannot be decrypted"
            });
        }
    }

    /// <summary>
    /// Derives a 256-bit key from machine identity + local master key seed via HKDF-SHA256.
    /// </summary>
    private static byte[] DeriveMachineBoundKey(string? customMasterKeyPath, out SeedOrigin origin, out string seedPath, out string? detail)
    {
        string machineId = GetHardwareMachineId();
        byte[] localSeed = GetOrCreateLocalMasterSeed(customMasterKeyPath, out origin, out seedPath, out detail);

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

    /// <summary>Smallest seed accepted from disk (128 bits); generated seeds are 256 bits.</summary>
    private const int MinSeedBytes = 16;

    /// <summary>
    /// Reads the master seed, validating it instead of trusting whatever is on disk: an empty,
    /// too short or zero-filled file (what a power loss or BSOD can leave behind) would otherwise
    /// silently become the key material. A damaged seed is restored from <c>master.seed.bak</c>; only
    /// when neither file is usable is a new seed generated, and the caller is told so.
    /// New seeds are written durably (temp file, flush, atomic rename) together with their backup.
    /// </summary>
    private static byte[] GetOrCreateLocalMasterSeed(string? customPath, out SeedOrigin origin, out string seedPath, out string? detail)
    {
        seedPath = customPath ?? (RuntimeInformation.IsOSPlatform(OSPlatform.Windows)
            ? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "Heimdall", "master.seed")
            : "/etc/heimdall/master.seed");
        string backupPath = AtomicFile.BackupPathFor(seedPath);

        var dir = Path.GetDirectoryName(seedPath);
        if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
        {
            Directory.CreateDirectory(dir);
        }

        if (TryReadSeed(seedPath, out var seed, out detail))
        {
            origin = SeedOrigin.Loaded;
            if (!File.Exists(backupPath))
            {
                TryWriteSeed(backupPath, seed); // seeds created by earlier versions had no backup
            }
            return seed;
        }

        bool primaryWasDamaged = detail != "missing";
        if (TryReadSeed(backupPath, out seed, out _))
        {
            origin = SeedOrigin.RestoredFromBackup;
            TryWriteSeed(seedPath, seed);
            return seed;
        }

        byte[] newSeed = RandomNumberGenerator.GetBytes(32);
        AtomicFile.Write(seedPath, newSeed, keepPreviousAsBackup: false, ownerOnly: true);
        TryWriteSeed(backupPath, newSeed);
        origin = primaryWasDamaged ? SeedOrigin.RegeneratedAfterCorruption : SeedOrigin.Created;
        return newSeed;
    }

    private static bool TryReadSeed(string path, out byte[] seed, out string? defect)
    {
        // Binary content: only "absent", "too short" and "all zeros" can be judged (a random seed
        // legitimately contains 0x00 bytes, so the text heuristics of AtomicFile do not apply).
        seed = Array.Empty<byte>();
        try
        {
            if (!File.Exists(path))
            {
                defect = "missing";
                return false;
            }
            seed = File.ReadAllBytes(path);
        }
        catch
        {
            defect = "unreadable";
            return false;
        }

        if (seed.Length == 0)
        {
            defect = "empty";
            return false;
        }

        if (Array.TrueForAll(seed, b => b == 0))
        {
            defect = "zero_filled";
            return false;
        }

        if (seed.Length < MinSeedBytes)
        {
            defect = "truncated";
            return false;
        }

        defect = null;
        return true;
    }

    private static void TryWriteSeed(string path, byte[] seed)
    {
        try
        {
            AtomicFile.Write(path, seed, keepPreviousAsBackup: false, ownerOnly: true);
        }
        catch
        {
            // Read-only location: the seed in memory is still valid for this run.
        }
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
