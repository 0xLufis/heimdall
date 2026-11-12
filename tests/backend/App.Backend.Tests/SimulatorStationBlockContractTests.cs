namespace App.Backend.Tests;

using System.Text.Json;
using App.Backend.Api.Services;
using App.Shared.Protos.Telemetry;
using Xunit;

/// <summary>
/// Cross-language contract: the bytes the Python fleet simulator publishes on
/// <c>heimdall/devices/{id}/plc_memory</c> (committed fixture, regenerated and guarded by
/// <c>simulators/fleet/test_contract_fixture.py</c>) must be fully understood by the real
/// <see cref="StationTelemetryExtractor"/>. A renamed PLC field or a new alias on either side
/// fails here instead of silently producing stations without telemetry in the dev environment.
/// </summary>
public class SimulatorStationBlockContractTests
{
    private sealed record FixtureBlock(string StationId, string MachineType, string State, List<string> NumericFields, string BlockBase64);
    private sealed record Fixture(List<FixtureBlock> Blocks);

    private static Fixture LoadFixture()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null && !File.Exists(Path.Combine(dir.FullName, "Heimdall.sln"))) dir = dir.Parent;
        Assert.True(dir != null, "Repository root (Heimdall.sln) not found above the test output directory.");

        var path = Path.Combine(dir!.FullName, "tests", "fixtures", "simulator", "station_blocks.json");
        Assert.True(File.Exists(path), $"Missing {path}. Generate it with: venv/bin/python simulators/fleet/contract_fixture.py --write");

        var fixture = JsonSerializer.Deserialize<Fixture>(File.ReadAllText(path), new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        Assert.NotNull(fixture);
        Assert.NotEmpty(fixture!.Blocks);
        return fixture;
    }

    public static IEnumerable<object[]> StationIds() => LoadFixture().Blocks.Select(b => new object[] { b.StationId });

    [Theory]
    [MemberData(nameof(StationIds))]
    public void SimulatorBlock_IsFullyRecognisedByTheExtractor(string stationId)
    {
        var expected = LoadFixture().Blocks.Single(b => b.StationId == stationId);
        var block = AdsPlcMemoryBlock.Parser.ParseFrom(Convert.FromBase64String(expected.BlockBase64));

        // Station attribution used by TelemetryIngestionService.ResolveStationAsync
        Assert.True(block.Metadata.TryGetValue("StationId", out var attributedTo));
        Assert.Equal(stationId, attributedTo);
        Assert.Equal(TelemetryValue.ValueOneofCase.StructValue, block.StructuredValue.ValueCase);

        var sample = StationTelemetryExtractor.Extract(block, out var metrics);

        Assert.NotNull(sample);
        Assert.True(sample!.HasDynamics, "no machine dynamics were extracted");
        Assert.True(sample.HasProduction, "no production values were extracted");
        // The simulator's state label maps onto the HUD vocabulary unchanged.
        Assert.Equal(expected.State, sample.OperationalStatus);
        Assert.NotNull(sample.LastTelemetryAt);

        // Every numeric value the simulator sent became exactly one metric: nothing was dropped
        // as an unknown field name and nothing was matched twice.
        Assert.Equal(expected.NumericFields.Count, metrics.Count);
        Assert.Equal(metrics.Count, metrics.Select(m => m.Metric).Distinct().Count());
        Assert.All(metrics, m => Assert.True(double.IsFinite(m.Value), $"{m.Metric} is not finite"));

        // Values needed by the station KPI tiles and the live telemetry page.
        Assert.NotNull(sample.CycleTimeMs);
        Assert.NotNull(sample.TemperatureC);
        Assert.NotNull(sample.PowerKw);
        Assert.NotNull(sample.Availability);
        Assert.NotNull(sample.Performance);
        Assert.NotNull(sample.Quality);
        Assert.NotNull(sample.PartsPerHour);
        Assert.NotNull(sample.TargetPartsPerHour);
        Assert.NotNull(sample.TargetCycleTimeSeconds);
        Assert.NotNull(sample.TotalPartsProduced);
        Assert.NotNull(sample.ScrapParts);
        Assert.InRange(sample.Availability!.Value, 0, 100);
        Assert.InRange(sample.Quality!.Value, 0, 100);
        Assert.True(sample.ScrapParts!.Value <= sample.TotalPartsProduced!.Value);
    }

    [Fact]
    public void Fixture_CoversProducingFaultedAndMaintenanceStations()
    {
        var states = LoadFixture().Blocks.Select(b => b.State).ToHashSet();
        Assert.Contains("FAULTED", states);
        Assert.Contains("MAINTENANCE", states);
        Assert.Contains("RUNNING", states);
    }
}
