# Agent Infrastructure

*Note: This section is currently under development as part of the Phase 3 documentation effort.*

The Heimdall Agent is a C# background worker service designed to run on factory floor PCs.

## Core Responsibilities
1. **Telemetry Collection**: Polling system resources (CPU, RAM, Disk) and reporting via the Heartbeat API.
2. **Command Processing**: Polling the command queue and executing remote instructions (e.g., config updates, file checks).
3. **Event Logging**: Capturing local system events and forwarding them to the central Core.

## Configuration
The agent is configured via a local `.env` file or command-line arguments, specifying the central Core URL and authentication credentials.

## Future Roadmap
* Remote shell execution support.
* Binary update orchestration.
* PLC register synchronization (OPC-UA).