using FluentValidation;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

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
            // порядок behaviors (AddOpenBehavior) додамо в кроці 4
        });

        services.AddValidatorsFromAssembly(assembly);            // усі AbstractValidator<T> із цієї збірки

        return services;
    }
}