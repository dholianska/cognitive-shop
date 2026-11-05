using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Reviews.Application.Common.Interfaces;
using Reviews.Domain.Interfaces;
using Reviews.Infrastructure.Persistence;
using Reviews.Infrastructure.Seeding;

namespace Reviews.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration config)
    {
        MongoMappings.Register();   // один раз, до першого звернення до бази

        services.Configure<MongoDbOptions>(config.GetSection(MongoDbOptions.SectionName));
        services.AddSingleton<MongoDbContext>();

        services.AddScoped<IReviewRepository, ReviewRepository>();
        services.AddScoped<IReviewQueries, ReviewQueries>();

        services.AddSingleton<IDataSeeder, ReviewSeeder>();

        // hosted services стартують у порядку реєстрації: спершу індекси, потім сідінг
        services.AddHostedService<IndexCreationService>();
        services.AddHostedService<DataSeedingService>();

        return services;
    }
}