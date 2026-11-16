namespace Auth.Api.Services;

/// <summary>Імітація відправки пошти для розробки: лист виводиться в лог.</summary>
public sealed class MockEmailService(ILogger<MockEmailService> logger) : IEmailService
{
    public Task SendAsync(string to, string subject, string body, CancellationToken ct = default)
    {
        logger.LogInformation("MOCK EMAIL to {To} | {Subject}{NewLine}{Body}", to, subject, Environment.NewLine, body);
        return Task.CompletedTask;
    }
}