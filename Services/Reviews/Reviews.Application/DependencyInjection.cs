using FluentValidation;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Reviews.Application.Behaviors;

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
            cfg.AddOpenBehavior(typeof(LoggingBehavior<,>));      // 1: логує початок, успіх чи збій
            cfg.AddOpenBehavior(typeof(ValidationBehavior<,>));   // 2: невалідний запит до обробника не дійде
            cfg.AddOpenBehavior(typeof(PerformanceBehavior<,>));  // 3: міряє час саме обробника
        });

        services.AddValidatorsFromAssembly(assembly);             // усі AbstractValidator<T> із цієї збірки

        return services;
    }
}