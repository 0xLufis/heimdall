# Heimdall Industrial Protocols & Adapters Technical Manual

This manual provides technical details and first-party reference specifications for all industrial protocols, buses, and hardware diagnostics implemented in the Heimdall Edge Agent.

---

## 1. Beckhoff TwinCAT ADS & EtherCAT Diagnostics

### 1.1 Cross-Platform ADS in .NET 9
- Uses `Beckhoff.TwinCAT.Ads` and in-process `AmsTcpIpRouter` for Linux/Docker deployments.
- **Port 10000**: TwinCAT System Service (Run/Config/Stop mode).
- **Port 851**: TwinCAT 3 PLC Runtime 1.
- **ADS Sum Commands (`0xF080`)**: Bundles hundreds of tag reads/writes into **1 single TCP round-trip packet**.

### 1.2 EtherCAT Master & Slave Diagnostic Registers
- **ADS Port `0xFFFF`**: Direct access to EtherCAT Master.
- **IndexGroup `0x00000003`, Offset `0x00000100`**: Master State (`0x1`=INIT, `0x2`=PREOP, `0x3`=BOOT, `0x4`=SAFEOP, `0x8`=OP).
- **Master `DevState` Bitmask**:
  - `0x0001`: Physical link error on primary port.
  - `0x0008`: Missing cyclic frames.
  - `0x0800`: Slave in error state.
  - `0x1000`: Distributed Clocks (DC) out of sync.
- **Slave `ST_EcSlaveState` (`0x00000009`)**: Full topology table of all configured slaves and link states.
- **Working Counter (`WcState`)**: `0` = Valid, `1` = Working Counter telegram failure.
- **CoE SDO Upload/Download (`0xF302`)**: IndexOffset encodes `(Index << 16) | SubIndex`.
- **ESC Hardware Error Registers**: `0x0300..0x0307` per-port hardware CRC error counters.

### 1.3 TcOpen `ITcoHeimdallTelemetry` & Standalone `FB_HeimdallTelemetryBridge`
- **TcOpen OOP Component Architecture**:
  - `ITcoHeimdallTelemetry` extends `TcoCore.ITcoComponent` and defines `METHOD UpdateTelemetry : BOOL` and `PROPERTY SequenceId : ULINT`.
  - Concrete station blocks (e.g. `TcoStation1Telemetry`) output a strongly-typed `_data` variable containing pure IEC standard types (scalars, DUTs, flat arrays).
  - **Zero Dynamic Pointer Traversal**: The Heimdall Agent reads `<SymbolPath>._data` directly in 1 single ADS Sum Command. It never attempts to traverse `POINTER TO` or `REFERENCE TO` fields across the network.
- **Standalone TwinCAT 3 Double-Buffered Bridge**:
  - `FB_HeimdallTelemetryBridge` provides lock-free, atomic double-buffering (`stTelemetryBuffer[0..1]`) with alternating sequence counters (`nSequenceCounter`), ensuring external ADS clients read consistent snapshots without PLC cycle jitter.

---

## 2. Fieldbus Diagnostics (PROFINET, PROFIBUS, CANopen, EtherNet/IP)

| Protocol | Master State Variable | Slave State Variable | Acyclic Diagnostic Channel |
| :--- | :--- | :--- | :--- |
| **PROFINET RT / IRT** | `DevState` (`0x0001`: Device Error, `0x0004`: AR Error) | `BoxState` (`0x0000`: OK, `0x0004`: AR Fault) | `RDREC` Diagnostic Records (`0x800A` / `0x800C`) |
| **PROFIBUS DP** | `DevState` (`0x0001`: Bus Error, `0x0008`: Token Broken) | `BoxState` (`0x0001`: Station Timeout) | DPV1 Read/Write Services |
| **CANopen** | `DevState` (`0x0001`: Bus Off, `0x0004`: Tx Overflow) | `BoxState` (`0x0001`: Heartbeat Error) | CANopen SDO via ADS `0xF302` |
| **EtherNet/IP** | `DevState` (`0x0001`: Scanner Error, `0x0002`: IP Conflict) | `BoxState` (`0x0001`: No Connection) | CIP Explicit Messaging via ADS `0xF400` |

---

## 3. OPC UA Foundation (`OPCFoundation.NetStandard.Opc.Ua`)

- **Security Profiles**: `Basic256Sha256`, `Aes128_Sha256_RsaOaep` with X.509 application certificate store.
- **Session Reconnect Handler**: Active keepalive monitoring with automatic reconnect loop on PLC reboots.
- **Continuation Points**: Automated pagination when browsing large address space subtrees.
- **Monitored Items**: Server-side analog deadband filters (`DeadbandType.Absolute` / `Percent`).
- **Batching**: Automatic chunking to honor server's `OperationLimits.MaxNodesPerRead`.

---

## 4. Modbus TCP & Socket Stream Probes

- **Function Codes**: FC01 (Coils), FC02 (Discrete Inputs), FC03 (Holding Registers), FC04 (Input Registers), FC16 (Write Multiple).
- **Endianness Conversion**:
  - `ABCD`: Big-Endian (Siemens S7).
  - `CDAB`: Mid-Big Endian / Word-Swapped (de facto standard for 32-bit floats in Schneider Electric, ABB, and industrial instruments).
  - `BADC`: Mid-Little Endian.
  - `DCBA`: Little-Endian (Intel).
- **Register Block Batch Optimizer**: Groups adjacent register requests with gaps $\le 5$ into a single read command to reduce latency.
- **Raw Socket Probe**: Zero-copy stream framing via `System.IO.Pipelines` with OS-level `TcpKeepAlive`.
