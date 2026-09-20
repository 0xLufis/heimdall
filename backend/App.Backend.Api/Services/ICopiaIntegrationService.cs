namespace App.Backend.Api.Services;

using System.Threading;
using System.Threading.Tasks;

/// <summary>
/// Interface for Copia Automation Webhook Service.
/// Follows Interface-Implementation model and Dependency Inversion Principle.
/// </summary>
public interface ICopiaIntegrationService
{
    bool VerifyWebhookSignature(string payload, string signatureHeader, string secret);
    Task<bool> ProcessWebhookAsync(string eventType, string jsonPayload, string? signature = null, string? secret = null, CancellationToken cancellationToken = default);
}
