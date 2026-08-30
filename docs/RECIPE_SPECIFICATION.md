# Heimdall Declarative Recipe Specification

This specification documents the schema, data typings, scheduling strategies, deadband models, and cryptographic signing formats for Heimdall Declarative Recipes.

---

## 1. Declarative Recipe JSON Schema

```json
{
  "$schema": "https://json-schema.org/draft/2020-12/schema",
  "title": "HeimdallRecipe",
  "type": "object",
  "required": ["recipeId", "version", "name", "targetSelector", "dataPoints", "security"],
  "properties": {
    "recipeId": { "type": "string", "format": "uuid" },
    "version": { "type": "string", "pattern": "^[0-9]+\\.[0-9]+\\.[0-9]+$" },
    "name": { "type": "string" },
    "description": { "type": "string" },
    "targetSelector": {
      "type": "object",
      "properties": {
        "osPlatform": { "type": "string", "enum": ["Windows", "Linux", "Any"] },
        "controllerRoles": { "type": "array", "items": { "type": "string" } },
        "tags": { "type": "object", "additionalProperties": { "type": "string" } }
      }
    },
    "security": {
      "type": "object",
      "required": ["keyId", "algorithm", "signature", "canonicalHash"],
      "properties": {
        "keyId": { "type": "string" },
        "algorithm": { "type": "string", "enum": ["RSA-SHA256", "Ed25519"] },
        "signature": { "type": "string" },
        "canonicalHash": { "type": "string" },
        "isEncrypted": { "type": "boolean" },
        "encryptionNonce": { "type": "string" },
        "encryptionTag": { "type": "string" }
      }
    },
    "dataPoints": {
      "type": "array",
      "items": {
        "$ref": "#/$defs/DataPointDefinition"
      }
    }
  }
}
```

---

## 2. Data Categories & Supported Protocol Drivers

### Data Categories
* **`Scalar`**: Single primitive values (`Int32`, `Float64`, `Boolean`, `String`, `DateTimeOffset`).
* **`List`**: Sequences of records (e.g. process lists, network routes, installed software packages).
* **`Map`**: Key-value dictionaries (e.g. disk metrics by drive letter, CPU core temperatures).
* **`NestedObject`**: Complex hierarchical structures (e.g. PLC DUTs, WMI hardware graphs).
* **`Metric`**: Continuous telemetry time-series (e.g. spindle RPM, motor current, pressure).
* **`DeviceState`**: Finite state machine enums (e.g. `INIT`, `PREOP`, `SAFEOP`, `OP`, `FAULT`).

### Protocol Driver Configurations
1. **`Beckhoff.Ads`**: `{ "amsNetId": "192.168.1.100.1.1", "port": 851, "symbolName": "MAIN.stSpindle.fActualSpeed" }`
2. **`Beckhoff.EtherCAT`**: `{ "masterAmsNetId": "192.168.1.100.1.1", "masterInstanceId": 0, "inspectCrcErrors": true }`
3. **`System.Cim`**: `{ "namespace": "root/cimv2", "wqlQuery": "SELECT FreePhysicalMemory, TotalVisibleMemorySize FROM Win32_OperatingSystem" }`
4. **`System.Process`**: `{ "processNamePattern": "TcXaeShell*", "depth": "ModulesAndThreads" }`
5. **`System.Disk`**: `{ "includePhysicalDrives": true, "includeSmart": true }`
6. **`System.FileSystem`**: `{ "targetPath": "C:\\TwinCAT\\3.1\\Boot", "fileFilter": "*.tszip", "calculateSha256": true }`
7. **`OpcUa.Subscription`**: `{ "endpointUrl": "opc.tcp://192.168.1.10:4840", "nodeId": "ns=2;s=Spindle.Speed" }`
8. **`Modbus.Tcp`**: `{ "ipAddress": "192.168.1.20", "port": 502, "unitId": 1, "functionCode": 3, "registerAddress": 40001, "byteOrder": "CDAB" }`

---

## 3. Polling Strategies & Deadband Filtering

### Strategy Types
* **`Periodic`**: Fixed interval polling (`intervalMs: 250`).
* **`Cron`**: Scheduled cron execution (`cronExpression: "0 */5 * * *"`).
* **`ChangeOfValue`**: Event-driven notification triggered by value change.
* **`OnDemand`**: Triggered only upon explicit central command request.

### Deadband Types
* **`Absolute`**: Emits only if $|V_{\text{new}} - V_{\text{last}}| \ge \text{Threshold}$.
* **`Percentage`**: Emits only if $\frac{|V_{\text{new}} - V_{\text{last}}|}{|V_{\text{last}}|} \times 100 \ge \text{Threshold}\%$.
* **`StateChangeOnly`**: Emits upon value change or `xxHash64` object hash change.
* **`MaxQuietPeriodMs`**: Maximum heartbeat TTL (e.g. 900,000 ms = 15 min) forcing a fresh baseline reading.
