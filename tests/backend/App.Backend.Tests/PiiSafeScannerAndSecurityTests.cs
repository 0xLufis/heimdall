using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using App.Agent.Daemon.Infrastructure.FileSystem;
using App.Agent.Daemon.Infrastructure.Security;
using Xunit;

namespace App.Backend.Tests;

public class PiiSafeScannerAndSecurityTests
{
    [Theory]
    [InlineData(@"C:\Users\JohnDoe\AppData\Local\Google\Chrome\User Data", true)]
    [InlineData(@"C:\Users\JohnDoe\AppData\Local\Microsoft\Edge\User Data", true)]
    [InlineData(@"/home/operator/.config/google-chrome/Default", true)]
    [InlineData(@"C:\Users\JohnDoe\AppData\Roaming\Mozilla\Firefox\Profiles", true)]
    [InlineData(@"C:\Program Files\App\node_modules\package", true)]
    [InlineData(@"C:\Projects\Heimdall\.git\objects", true)]
    [InlineData(@"C:\TwinCAT\3.1\Boot", false)]
    [InlineData(@"/opt/heimdall/recipes", false)]
    [InlineData(@"C:\ProgramData\Siemens\Automation", false)]
    public void FileScanner_CorrectlyIdentifiesBlacklistedDirectories(string path, bool expectedBlacklisted)
    {
        bool isBlacklisted = SecureIndustrialFileScanner.IsDirectoryBlacklisted(path);
        Assert.Equal(expectedBlacklisted, isBlacklisted);
    }

    [Fact]
    public void ProcessSecretScrubber_RedactsPasswordsTokensAndUserAccounts()
    {
        string rawCommandLine = "myapp.exe --host 192.168.1.10 --password Secret123! -H \"Authorization: Bearer eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9.e30.t-ID\" --path C:\\Users\\JohnDoe\\config.json";

        string scrubbed = ProcessSecretScrubber.Scrub(rawCommandLine);

        Assert.DoesNotContain("Secret123!", scrubbed);
        Assert.Contains("--password [REDACTED]", scrubbed);
        Assert.DoesNotContain("eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9.e30.t-ID", scrubbed);
        Assert.Contains("[REDACTED_JWT]", scrubbed);
        Assert.DoesNotContain("JohnDoe", scrubbed);
        Assert.Contains("C:\\Users\\[USER_ACCOUNT]\\config.json", scrubbed);
    }

    [Fact]
    public void CrossPlatformSecureStorage_EncryptsAndDecryptsWithAuthTag()
    {
        string tempMasterSeedPath = Path.Combine(Path.GetTempPath(), $"heimdall_test_{Guid.NewGuid():N}.seed");

        try
        {
            var storage = new CrossPlatformSecureStorage(tempMasterSeedPath);
            string secretConfig = "{\"amsNetId\":\"192.168.1.100.1.1\",\"dbPassword\":\"SuperIndustrialSecret99!\"}";

            byte[] encrypted = storage.Encrypt(Encoding.UTF8.GetBytes(secretConfig));
            Assert.NotEmpty(encrypted);

            // Verify payload is not plaintext
            Assert.DoesNotContain("SuperIndustrialSecret99!", Encoding.UTF8.GetString(encrypted));

            // Decrypt and verify
            byte[] decryptedBytes = storage.Decrypt(encrypted);
            string decrypted = Encoding.UTF8.GetString(decryptedBytes);
            Assert.Equal(secretConfig, decrypted);

            // Tamper test: Altering a single byte in ciphertext must cause CryptographicException on tag verification
            encrypted[encrypted.Length - 1] ^= 0xFF;
            Assert.ThrowsAny<CryptographicException>(() => storage.Decrypt(encrypted));
        }
        finally
        {
            if (File.Exists(tempMasterSeedPath))
            {
                File.Delete(tempMasterSeedPath);
            }
        }
    }
}
