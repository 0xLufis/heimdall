using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading.Tasks;
using App.Agent.Daemon;
using App.Agent.Daemon.CommandHandling;
using App.Agent.Daemon.Interfaces;
using App.Agent.Daemon.Reporting;
using App.Shared.Drivers;
using App.Shared.Errors;
using App.Shared.Protos;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace App.Backend.Tests;

public class AgentReportingTests
{
    private class TestConfigService : IConfigurationService
    {
        public AgentConfig Config { get; set; } = new AgentConfig { AllowRemoteExecution = true };
        public bool VerifyResult { get; set; } = true;

        public bool VerifyCommandSignature(ServerCommand command) => VerifyResult;
        public bool UpdateConfigSigned(string payload, string? signature) => true;
        public void LoadConfig() { }
        public void SaveConfig(AgentConfig config) { }
    }

    private class TestFileSystemScanner : IFileSystemScanner
    {
        public IEnumerable<DiscoveredAsset> ScanDirectory(string rootPath, System.Threading.CancellationToken ct = default)
        {
            return Array.Empty<DiscoveredAsset>();
        }
    }

    [Fact]
    public void DriversComponentContributor_ProducesStructuredDriverInventory()
    {
        var contributor = new DriversComponentContributor();
        var data = new SystemInfoData
        {
            Hostname = "TEST-NODE",
            Hardware = new HardwareInfo
            {
                Drivers = new List<DeviceDriver>
                {
                    new DeviceDriver
                    {
                        DeviceName = "Fieldbus Interface Card",
                        Service = "fb_rt",
                        DriverVersion = "2.1.0",
                        Provider = "Hardware Automation",
                        HardwareId = @"PCI\VEN_8086&DEV_1234",
                        DeviceId = @"PCI\VEN_8086&DEV_1234\0001",
                        InfName = "oem10.inf",
                        Category = "Fieldbus",
                        Status = "Running",
                        IsBound = true
                    }
                }
            }
        };

        var component = contributor.CreateComponent(data);

        Assert.NotNull(component);
        Assert.Equal("Drivers", component.Name);
        Assert.Equal("Kernel", component.Technology);
        Assert.Equal("driver", component.Type);

        var deserialized = JsonSerializer.Deserialize<List<DeviceDriver>>(component.DataJson);
        Assert.NotNull(deserialized);
        Assert.Single(deserialized);
        Assert.Equal("Fieldbus Interface Card", deserialized[0].DeviceName);
        Assert.Equal(@"PCI\VEN_8086&DEV_1234", deserialized[0].HardwareId);
        Assert.Equal("Fieldbus", deserialized[0].Category);
    }

    [Fact]
    public void PhysicalDrivesComponentContributor_ProducesPhysicalDrivesInventory()
    {
        var contributor = new PhysicalDrivesComponentContributor();
        var data = new SystemInfoData
        {
            Hostname = "TEST-NODE",
            Disk = new DiskData
            {
                PhysicalDrives = new List<PhysicalDriveInfo>
                {
                    new PhysicalDriveInfo
                    {
                        Model = "Samsung SSD 970 EVO 1TB",
                        SerialNumber = "S4EVNX0M123456",
                        SizeBytes = 1000204886016,
                        InterfaceType = "NVMe"
                    }
                }
            }
        };

        var component = contributor.CreateComponent(data);

        Assert.NotNull(component);
        Assert.Equal("PhysicalDrives", component.Name);
        Assert.Equal("Agent", component.Technology);
        Assert.Equal("hardware", component.Type);

        var drives = JsonSerializer.Deserialize<List<PhysicalDriveInfo>>(component.DataJson);
        Assert.NotNull(drives);
        Assert.Single(drives);
        Assert.Equal("Samsung SSD 970 EVO 1TB", drives[0].Model);
        Assert.Equal("NVMe", drives[0].InterfaceType);
    }

    [Fact]
    public async Task CommandHandler_RejectsPathTraversal_InFileCheck()
    {
        var configService = new TestConfigService();
        var fileScanner = new TestFileSystemScanner();
        var handler = new CommandHandler(configService, fileScanner, NullLogger<CommandHandler>.Instance);

        var command = new ServerCommand
        {
            Type = "FILE_CHECK",
            Payload = "../../../etc/shadow"
        };

        var result = await handler.HandleCommandAsync(command);

        Assert.False(result.Success);
        Assert.Equal(ErrorCode.PathTraversalDetected, result.ErrorCode);
    }

    [Fact]
    public async Task CommandHandler_RejectsExecutableExtension_InFileCheck()
    {
        var configService = new TestConfigService();
        var fileScanner = new TestFileSystemScanner();
        var handler = new CommandHandler(configService, fileScanner, NullLogger<CommandHandler>.Instance);

        var command = new ServerCommand
        {
            Type = "FILE_CHECK",
            Payload = "/tmp/exploit.bat"
        };

        var result = await handler.HandleCommandAsync(command);

        Assert.False(result.Success);
        Assert.Equal(ErrorCode.UnsafeCommandDetected, result.ErrorCode);
    }

    [Fact]
    public async Task CommandHandler_RejectsShellChaining_InShellExec()
    {
        var configService = new TestConfigService();
        var fileScanner = new TestFileSystemScanner();
        var handler = new CommandHandler(configService, fileScanner, NullLogger<CommandHandler>.Instance);

        var command = new ServerCommand
        {
            Type = "SHELL_EXEC",
            Payload = "echo safe; rm -rf /"
        };

        var result = await handler.HandleCommandAsync(command);

        Assert.False(result.Success);
        Assert.Equal(ErrorCode.UnsafeCommandDetected, result.ErrorCode);
    }

    [Fact]
    public async Task CommandHandler_RejectsUnsignedCommands()
    {
        var configService = new TestConfigService { VerifyResult = false };
        var fileScanner = new TestFileSystemScanner();
        var handler = new CommandHandler(configService, fileScanner, NullLogger<CommandHandler>.Instance);

        var command = new ServerCommand
        {
            Type = "UPDATE_CONFIG",
            Payload = "{}"
        };

        var result = await handler.HandleCommandAsync(command);

        Assert.False(result.Success);
        Assert.Equal(ErrorCode.SignatureVerificationFailed, result.ErrorCode);
    }

    [Fact]
    public void ApiError_SerializationAndFormatting_PreservesCodesAndContext()
    {
        var error = new ApiError(
            ErrorCode.PathTraversalDetected,
            message: "Target path contains illegal directory traversal sequences.",
            details: "AttemptedPath=../../../etc/shadow"
        );

        Assert.Equal(ErrorCode.PathTraversalDetected, error.Code);
        Assert.Equal("PathTraversalDetected", error.CodeName);
        Assert.NotNull(error.Details);

        string json = JsonSerializer.Serialize(error);
        Assert.Contains("\"Code\":1003", json);
        Assert.Contains("\"CodeName\":\"PathTraversalDetected\"", json);
        Assert.Contains("AttemptedPath", json);
    }

    [Fact]
    public void ApiError_Constructors_ReturnStandardResponses()
    {
        var validationErr = new ApiError(ErrorCode.InvalidInput, details: "Hostname cannot be empty.");
        Assert.Equal(ErrorCode.InvalidInput, validationErr.Code);
        Assert.Equal("Invalid input parameter provided.", validationErr.Message);
        Assert.Equal("Hostname cannot be empty.", validationErr.Details);

        var notFoundErr = new ApiError(ErrorCode.EntityNotFound, details: "Client PC not found.");
        Assert.Equal(ErrorCode.EntityNotFound, notFoundErr.Code);
        Assert.Equal("The requested entity was not found.", notFoundErr.Message);

        var forbiddenErr = new ApiError(ErrorCode.AccessDenied, details: "Access denied to requested resource.");
        Assert.Equal(ErrorCode.AccessDenied, forbiddenErr.Code);
        Assert.Equal("Access denied for the requested operation.", forbiddenErr.Message);
    }
}
