using Grpc.Core;
using App.Shared.Protos;
using App.Shared.Entities;
using Google.Protobuf.WellKnownTypes;

namespace App.Backend.Api.Services;

public class SystemInfoCollectorService : SystemInfoCollector.SystemInfoCollectorBase
{
    private readonly ILogger<SystemInfoCollectorService> _logger;

    public SystemInfoCollectorService(ILogger<SystemInfoCollectorService> logger)
    {
        _logger = logger;
    }

    public override Task<SystemInfoResponse> ReportSystemInfo(SystemInfoRequest request, ServerCallContext context)
    {
        _logger.LogInformation("Received system info from {Hostname} ({MachineIdentifier})", 
            request.Hostname, request.MachineIdentifier);

        // TODO: Map gRPC request to ClientPc entity and save to PostgreSQL using AppDbContext
        // For now, just return success

        return Task.FromResult(new SystemInfoResponse
        {
            Success = true,
            Message = $"Information for {request.Hostname} received successfully."
        });
    }
}
