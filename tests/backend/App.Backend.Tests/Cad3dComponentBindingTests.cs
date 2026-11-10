namespace App.Backend.Tests;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Xunit;
using App.Contracts.Inventory;
using App.Contracts.Machine;
using App.Contracts.Templates;
using App.Infrastructure.Repositories;
using App.Infrastructure.Services;
using App.Shared.Data;
using App.Shared.Entities;

public class Cad3dComponentBindingTests
{
    [Fact]
    public void EplanCadReference_SerializesAndDeserializes_3dCadIdentities()
    {
        var original = new EplanCadReference
        {
            DeviceTag = "=LINE1+OP20-M1",
            CadHandle = "3B4F",
            CadBlockName = "MCH-SMT-PRINT",
            DrawingPage = "Page 12, Col 4",
            Model3dUrl = "/sample/station_digital_twin.glb",
            Cad3dNodePath = "GANTRY-X-01",
            Cad3dMeshId = "mesh_GANTRY-X-01",
            LocalTransform = new Cad3dTransform
            {
                X = 0.12,
                Y = 1.35,
                Z = -0.15,
                RotationX = 0.0,
                RotationY = 90.0,
                RotationZ = 0.0,
                Scale = 1.0
            },
            BoundingBox3d = new Cad3dBoundingBox
            {
                MinX = -0.55,
                MinY = 1.35,
                MinZ = -0.23,
                MaxX = 0.55,
                MaxY = 1.45,
                MaxZ = -0.13
            }
        };

        var json = JsonSerializer.Serialize(original);
        Assert.NotNull(json);
        Assert.Contains("GANTRY-X-01", json);
        Assert.Contains("mesh_GANTRY-X-01", json);
        Assert.Contains("/sample/station_digital_twin.glb", json);

        var deserialized = JsonSerializer.Deserialize<EplanCadReference>(json);
        Assert.NotNull(deserialized);
        Assert.Equal(original.DeviceTag, deserialized.DeviceTag);
        Assert.Equal(original.Model3dUrl, deserialized.Model3dUrl);
        Assert.Equal(original.Cad3dNodePath, deserialized.Cad3dNodePath);
        Assert.Equal(original.Cad3dMeshId, deserialized.Cad3dMeshId);

        Assert.NotNull(deserialized.LocalTransform);
        Assert.Equal(0.12, deserialized.LocalTransform.X);
        Assert.Equal(90.0, deserialized.LocalTransform.RotationY);

        Assert.NotNull(deserialized.BoundingBox3d);
        Assert.Equal(-0.55, deserialized.BoundingBox3d.MinX);
        Assert.Equal(0.55, deserialized.BoundingBox3d.MaxX);
    }

    [Fact]
    public void StationComponentTreeDto_IncludesModel3dUrl_AndInstalledPartsCadMetadata()
    {
        var tree = new StationComponentTreeDto
        {
            StationId = Guid.NewGuid(),
            StationName = "OP-020 SMT Paste Printer",
            CustomIdentifier = "L01-OP020",
            MachineType = "SMT",
            Model3dUrl = "/sample/station_digital_twin.glb",
            InstalledParts = new List<StationInstalledPartDto>
            {
                new()
                {
                    Id = Guid.NewGuid(),
                    Name = "Gantry Traverse Servo Motor",
                    SerialNumber = "SRV-9921",
                    EquipmentStatus = "InMachine",
                    Technology = "Kinematics",
                    Cad3dNodePath = "GANTRY-X-01",
                    FunctionalSlotIdentifier = "GANTRY-X-01",
                    CadReference = new EplanCadReference
                    {
                        DeviceTag = "=LINE1+OP20-M1",
                        Cad3dNodePath = "GANTRY-X-01",
                        Model3dUrl = "/sample/station_digital_twin.glb"
                    }
                },
                new()
                {
                    Id = Guid.NewGuid(),
                    Name = "High-Speed Piezo Dispenser Valve",
                    SerialNumber = "DISP-1140",
                    EquipmentStatus = "InMachine",
                    Technology = "Dispensing",
                    Cad3dNodePath = "TOOL-HEAD-01",
                    FunctionalSlotIdentifier = "TOOL-HEAD-01"
                }
            }
        };

        Assert.Equal("/sample/station_digital_twin.glb", tree.Model3dUrl);
        Assert.Equal(2, tree.InstalledParts.Count);
        Assert.Equal("GANTRY-X-01", tree.InstalledParts[0].Cad3dNodePath);
        Assert.Equal("TOOL-HEAD-01", tree.InstalledParts[1].Cad3dNodePath);
        Assert.NotNull(tree.InstalledParts[0].CadReference);
        Assert.Equal("=LINE1+OP20-M1", tree.InstalledParts[0].CadReference!.DeviceTag);
    }

    [Fact]
    public async Task StationComponentTree_BindsOnlyFromEngineeringData_AndUnknownStationIsNull()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"Cad3dTreeDb_{Guid.NewGuid():N}")
            .Options;
        using var context = new AppDbContext(options);

        var station = new Machine { Id = Guid.NewGuid(), Name = "OP20", CustomIdentifier = "OP20" };
        var servo = new HardwareComponent { Id = Guid.NewGuid(), Name = "Servo Drive", MachineId = station.Id, EquipmentStatus = "InMachine" };
        var camera = new HardwareComponent { Id = Guid.NewGuid(), Name = "Camera", MachineId = station.Id, EquipmentStatus = "InMachine" };
        context.Machines.Add(station);
        context.InventoryItems.AddRange(servo, camera);
        context.MachineComponents.Add(new MachineComponent
        {
            MachineId = station.Id,
            Name = "X axis",
            FunctionalSlotIdentifier = "SLOT-X",
            CurrentPartId = servo.Id,
            EplanCadRefJson = JsonSerializer.Serialize(new EplanCadReference { Cad3dNodePath = "GANTRY-X-01" })
        });
        await context.SaveChangesAsync();

        var repository = new AssetRepository(context);

        Assert.Null(await repository.GetStationComponentTreeAsync("UNKNOWN-STATION"));

        var tree = await repository.GetStationComponentTreeAsync(station.Id);
        Assert.NotNull(tree);
        Assert.Equal("GANTRY-X-01", tree!.InstalledParts.Single(p => p.Id == servo.Id).Cad3dNodePath);
        Assert.Equal("SLOT-X", tree.InstalledParts.Single(p => p.Id == servo.Id).FunctionalSlotIdentifier);
        // No engineering reference: no guessed mesh binding from the part name.
        Assert.Null(tree.InstalledParts.Single(p => p.Id == camera.Id).Cad3dNodePath);
    }
}
