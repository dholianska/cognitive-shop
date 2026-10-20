using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Reviews.Infrastructure.Persistence;

namespace Reviews.Infrastructure.Seeding;

public class DataSeedingService(
    IEnumerable<IDataSeeder> seeders,
    IOptions<MongoDbOptions> options,
    ILogger<DataSeedingService> logger) : IHostedService
{
    public async Task StartAsync(CancellationToken ct)
    {
        if (!options.Value.SeedOnStartup)
        {
            logger.LogInformation("Seeding is disabled (MongoDb:SeedOnStartup = false)");
            return;
        }

        foreach (var seeder in seeders)
            await seeder.SeedAsync(ct);
    }

    public Task StopAsync(CancellationToken ct) => Task.CompletedTask;
}