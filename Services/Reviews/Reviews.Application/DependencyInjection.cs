using FluentValidation;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Reviews.Application.Behaviors;
using Reviews.Application.Caching;

namespace Reviews.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services, IConfiguration config)
    {
        var assembly = typeof(DependencyInjection).Assembly;

        services.AddMediatR(cfg =>
        {
            cfg.LicenseKey = config["LuckyPenny:LicenseKey"];   // необов'язково: без ключа працює, лише попередження в лозі
            cfg.RegisterServicesFromAssembly(assembly);          // автоскан усіх IRequestHandler

            // порядок реєстрації = порядок виконання: перший зовнішній, останній біля обробника
            cfg.AddOpenBehavior(typeof(LoggingBehavior<,>));            // 1
            cfg.AddOpenBehavior(typeof(ValidationBehavior<,>));         // 2
            cfg.AddOpenBehavior(typeof(CachingBehavior<,>));            // 3: кеш запитів після валідації
            cfg.AddOpenBehavior(typeof(PerformanceBehavior<,>));        // 4
            cfg.AddOpenBehavior(typeof(CacheInvalidationBehavior<,>));  // 5: інвалідація після запису
        });

        services.AddValidatorsFromAssembly(assembly);             // усі AbstractValidator<T> із цієї збірки

        services.AddSingleton<ITwoLevelCache, TwoLevelCache>();
        services.AddSingleton<IListVersionStore, ListVersionStore>();

        return services;
    }
}