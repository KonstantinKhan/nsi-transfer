using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using NsiTransfer.Contract.ConfigModels;
using NsiTransfer.Contract.Interfaces;
using NsiTransfer.DAL.Db.Context;
using NsiTransfer.DAL.Interfaces.Db;
using NsiTransfer.DAL.Interfaces.Email;
using NsiTransfer.DAL.Interfaces.Http;
using NsiTransfer.DAL.Interfaces.MessageBrokers;
using NsiTransfer.DAL.Network.Email;
using NsiTransfer.DAL.Network.Http;
using NsiTransfer.DAL.Network.MessageBrokers;
using NsiTransfer.DAL.Repositories.Db;
using NsiTransfer.DAL.Repositories.Network;
using NsiTransfer.DAL.Services;

namespace NsiTransfer.DAL;

public static class HostExtensions
{
	public static IServiceCollection AddDalServices(this IServiceCollection services, IConfiguration configuration)
	{
        // Services setup
		services.AddSingleton<IPolynomAuthHttpRepository, PolynomAuthHttpRepository>();
		services.AddSingleton<IPolynomApiHttpRepository, PolynomApiHttpRepository>();
		services.AddSingleton<IPolynomHttpClientFactory, PolynomHttpClientFactory>();

        services.AddSingleton<IConfigurationWriter, JsonConfigurationWriter>();

        services.AddScoped<IMessageRepository, MessageRepository>();
        services.AddScoped(typeof(IGenericRepository<>), typeof(GenericRepository<>));
        services.AddScoped<IUnitOfWork, UnitOfWork>();


        // Http services setup
        services.AddTransient<PolynomSessionHttpClientHandler>();
		services.AddHttpClient<PolynomAuthHttpClient>((serviceProvider, client) =>
		{
			var config = serviceProvider.GetRequiredService<IOptionsMonitor<PolynomConfig>>().CurrentValue;

			if (!string.IsNullOrEmpty(config?.Address))
			{
				client.BaseAddress = new Uri(config.Address);
			}
			else
			{
				throw new InvalidOperationException("При подстановке адреса сервера Полинома не удалось получить из конфигурации значение адреса сервера.");
			}
		});

		services.AddHttpClient<PolynomApiHttpClient>((serviceProvider, client) =>
		{
			var config = serviceProvider.GetRequiredService<IOptionsMonitor<PolynomConfig>>().CurrentValue;

			if (!string.IsNullOrEmpty(config?.Address))
			{
				client.BaseAddress = new Uri(config.Address);
			}
            else
            {
                throw new InvalidOperationException("При подстановке адреса сервера Полинома не удалось получить из конфигурации значение адреса сервера.");
            }
        })
		.AddHttpMessageHandler<PolynomSessionHttpClientHandler>();

		// HttpContextAccessor для доступа к HttpContext из DelegatingHandler
		services.AddHttpContextAccessor();

		
		
		// Db setup
		var connectionString = configuration.GetConnectionString("DefaultConnection");
		services.AddDbContext<AppDbContext>((sp, options) =>
		{
			options.UseNpgsql(connectionString);
		});



		// RabbitMQ setup
		services.AddSingleton<IConnectionFactory, ConnectionFactoryCreator>();
		services.AddSingleton<IRabbitMqPublisher, RabbitMqPublisher>();


        // Smtp client setup
        services.AddTransient<IEmailSender, EmailSender>();

        return services;
	}

    public static IServiceProvider MigrateDbContext(this IServiceProvider services, IHostEnvironment env)
    {
        using var scope = services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        dbContext.Database.Migrate();
        return services;
    }
}
