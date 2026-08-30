namespace App.Agent.Daemon.Domain.Recipes;

using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

/// <summary>
/// Categorizes the structural shape of a collected data point.
/// </summary>
public enum DataCategory
{
    Scalar,
    List,
    Map,
    NestedObject,
    Metric,
    DeviceState
}

/// <summary>
/// Supported ingestion protocol and system drivers.
/// </summary>
public enum RecipeSourceType
{
    SystemCim,
    SystemProcess,
    SystemDisk,
    SystemFileSystem,
    BeckhoffAds,
    BeckhoffEtherCat,
    OpcUaSubscription,
    ModbusTcp,
    TcpSocket
}

/// <summary>
/// Strategy for scheduling probe execution.
/// </summary>
public enum PollingStrategyType
{
    Periodic,
    Cron,
    ChangeOfValue,
    OnDemand
}

/// <summary>
/// Deadband evaluation filtering algorithms.
/// </summary>
public enum DeadbandType
{
    None,
    Absolute,
    Percentage,
    StateChangeOnly
}

/// <summary>
/// 4-Tier egress priority channels.
/// </summary>
public enum EgressPriority
{
    P0_CriticalAlarm = 0,
    P1_HighOperational = 1,
    P2_MediumMetrics = 2,
    P3_LowInventory = 3
}

/// <summary>
/// Inspection depth for OS process telemetry.
/// </summary>
public enum InspectionDepth
{
    Basic = 1,
    MemoryAndHandles = 2,
    ModulesAndThreads = 3,
    FullDumpAnalysis = 4
}

/// <summary>
/// Declarative Recipe document describing a set of collection probes for an industrial endpoint.
/// </summary>
public record RecipeDocument(
    [property: JsonPropertyName("recipeId")] Guid RecipeId,
    [property: JsonPropertyName("version")] string Version,
    [property: JsonPropertyName("name")] string Name,
    [property: JsonPropertyName("description")] string? Description,
    [property: JsonPropertyName("targetSelector")] TargetSelector TargetSelector,
    [property: JsonPropertyName("security")] RecipeSecurity Security,
    [property: JsonPropertyName("dataPoints")] List<DataPointDefinition> DataPoints
);

public record TargetSelector(
    [property: JsonPropertyName("osPlatform")] string OsPlatform,
    [property: JsonPropertyName("controllerRoles")] List<string> ControllerRoles,
    [property: JsonPropertyName("tags")] Dictionary<string, string> Tags
);

public record RecipeSecurity(
    [property: JsonPropertyName("keyId")] string KeyId,
    [property: JsonPropertyName("algorithm")] string Algorithm,
    [property: JsonPropertyName("signature")] string Signature,
    [property: JsonPropertyName("canonicalHash")] string CanonicalHash,
    [property: JsonPropertyName("isEncrypted")] bool IsEncrypted = false,
    [property: JsonPropertyName("encryptionNonce")] string? EncryptionNonce = null,
    [property: JsonPropertyName("encryptionTag")] string? EncryptionTag = null
);

public record DataPointDefinition(
    [property: JsonPropertyName("pointId")] string PointId,
    [property: JsonPropertyName("name")] string Name,
    [property: JsonPropertyName("category")] DataCategory Category,
    [property: JsonPropertyName("sourceType")] RecipeSourceType SourceType,
    [property: JsonPropertyName("sourceConfig")] SourceConfigBase SourceConfig,
    [property: JsonPropertyName("pollingStrategy")] PollingStrategyConfig PollingStrategy,
    [property: JsonPropertyName("egressPolicy")] EgressPolicyConfig EgressPolicy
);

[JsonPolymorphic(TypeDiscriminatorPropertyName = "$sourceType")]
[JsonDerivedType(typeof(AdsSourceConfig), "BeckhoffAds")]
[JsonDerivedType(typeof(EtherCatSourceConfig), "BeckhoffEtherCat")]
[JsonDerivedType(typeof(CimSourceConfig), "SystemCim")]
[JsonDerivedType(typeof(ProcessSourceConfig), "SystemProcess")]
[JsonDerivedType(typeof(DiskSourceConfig), "SystemDisk")]
[JsonDerivedType(typeof(FileSystemSourceConfig), "SystemFileSystem")]
[JsonDerivedType(typeof(OpcUaSourceConfig), "OpcUaSubscription")]
[JsonDerivedType(typeof(ModbusTcpSourceConfig), "ModbusTcp")]
[JsonDerivedType(typeof(TcpSocketSourceConfig), "TcpSocket")]
public abstract record SourceConfigBase
{
    public abstract string GetCanonicalKey();
}

public record AdsSourceConfig(
    string AmsNetId,
    int Port,
    string SymbolName,
    uint? IndexGroup = null,
    uint? IndexOffset = null,
    string? DataTypeName = "REAL64"
) : SourceConfigBase
{
    public override string GetCanonicalKey() =>
        $"Beckhoff.Ads:{AmsNetId}:{Port}:{(string.IsNullOrEmpty(SymbolName) ? $"{IndexGroup}:{IndexOffset}" : SymbolName)}";
}

public record EtherCatSourceConfig(
    string MasterAmsNetId,
    uint MasterInstanceId = 0,
    bool InspectCrcErrors = true,
    bool InspectTopology = true,
    List<uint>? TargetSlaveIndices = null
) : SourceConfigBase
{
    public override string GetCanonicalKey() =>
        $"Beckhoff.EtherCAT:{MasterAmsNetId}:{MasterInstanceId}";
}

public record CimSourceConfig(
    string Namespace,
    string WqlQuery,
    List<string> ProjectedProperties
) : SourceConfigBase
{
    public override string GetCanonicalKey() =>
        $"System.Cim:{Namespace.ToLowerInvariant()}:{ExtractTargetTable(WqlQuery).ToLowerInvariant()}";

    private static string ExtractTargetTable(string query)
    {
        var parts = query.Split(new[] { "FROM ", "from " }, StringSplitOptions.RemoveEmptyEntries);
        return parts.Length > 1 ? parts[1].Trim().Split(' ')[0] : query;
    }
}

public record ProcessSourceConfig(
    string ProcessNamePattern,
    InspectionDepth Depth = InspectionDepth.Basic
) : SourceConfigBase
{
    public override string GetCanonicalKey() =>
        $"System.Process:{ProcessNamePattern.ToLowerInvariant()}";
}

public record DiskSourceConfig(
    bool IncludeSmart = false,
    bool IncludePhysicalDrives = true,
    List<string>? DriveLetters = null
) : SourceConfigBase
{
    public override string GetCanonicalKey() => "System.Disk:AllDrives";
}

public record FileSystemSourceConfig(
    string TargetPath,
    string FileFilter = "*.*",
    bool CalculateSha256 = true,
    bool Recursive = false
) : SourceConfigBase
{
    public override string GetCanonicalKey() =>
        $"System.FileSystem:{TargetPath.ToLowerInvariant()}:{FileFilter.ToLowerInvariant()}";
}

public record OpcUaSourceConfig(
    string EndpointUrl,
    string NodeId,
    int SamplingIntervalMs = 250
) : SourceConfigBase
{
    public override string GetCanonicalKey() =>
        $"OpcUa:{EndpointUrl.ToLowerInvariant()}:{NodeId}";
}

public record ModbusTcpSourceConfig(
    string IpAddress,
    int Port = 502,
    byte UnitId = 1,
    byte FunctionCode = 3,
    ushort RegisterAddress = 0,
    ushort Length = 2,
    string ByteOrder = "CDAB"
) : SourceConfigBase
{
    public override string GetCanonicalKey() =>
        $"Modbus.Tcp:{IpAddress}:{Port}:{UnitId}:FC{FunctionCode}:{RegisterAddress}:{Length}";
}

public record TcpSocketSourceConfig(
    string Host,
    int Port,
    int TimeoutMs = 2000,
    bool CheckTlsCert = false
) : SourceConfigBase
{
    public override string GetCanonicalKey() =>
        $"Tcp.Socket:{Host.ToLowerInvariant()}:{Port}";
}

public record PollingStrategyConfig(
    PollingStrategyType StrategyType,
    int IntervalMs = 1000,
    string? CronExpression = null,
    DeadbandConfig? Deadband = null,
    int MaxQuietPeriodMs = 900000 // 15 mins heartbeat
);

public record DeadbandConfig(
    DeadbandType DeadbandType,
    double Threshold
);

public record EgressPolicyConfig(
    EgressPriority Priority,
    bool DeltaOnly,
    AlarmTriggerConfig? AlarmTrigger = null
);

public record AlarmTriggerConfig(
    string ConditionExpression,
    EgressPriority ElevateToPriority = EgressPriority.P0_CriticalAlarm
);
