using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using NsiTransfer.BLL.BackgroundServices;
using NsiTransfer.BLL.Interfaces.Services;
using NsiTransfer.BLL.Interfaces.UseCases;
using NsiTransfer.BLL.Queues;
using NsiTransfer.BLL.Services;
using NsiTransfer.BLL.Tools;
using NsiTransfer.BLL.UseCases;
using NsiTransfer.Contract.Interfaces;

namespace NsiTransfer.BLL;

public static class HostExtensions
{
    public static IServiceCollection AddBllServices(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddSingleton(typeof(IOptionsHelper<>), typeof(OptionsHelper<>));
        services.AddSingleton<PolynomTimeConverter>();
        services.AddSingleton<SessionManager>();
        services.AddSingleton<SessionStore>();
        services.AddSingleton<IPolynomTokenResolver, PolynomTokenResolver>();
        services.AddSingleton<IBackgroundTaskQueue, EmailTaskQueue>();
        services.AddSingleton<IBackgroundTaskQueue, SyncTaskQueue>();
        services.AddSingleton<ISyncNotifier, SyncNotifier>();

        services.AddScoped<ISyncUseCases, SyncUseCases>();
        services.AddScoped<IAdminPanelUseCases, AdminPanelUseCases>();
        services.AddScoped<IPolynomUtilsUseCases, PolynomUtilsUseCases>();

        services.AddScoped<IPolynomApiService, PolynomApiService>();
        services.AddScoped<IPolynomRequestBuilder, PolynomRequestBuilder>();
        services.AddScoped<IClassificationCodeProcessor, ClassificationCodeProcessor>();
        services.AddScoped<IErrorNotifier, ErrorNotifier>();

        services.AddTransient<IEmailService, EmailService>();

        services.AddHostedService<EmailTaskConsumer>();
        services.AddHostedService<SyncTaskConsumer>();

        return services;
    }
}
