﻿using Microsoft.Extensions.Options;
using NsiTransfer.AppConfig;
using NsiTransfer.Contract.ConfigModels;
using NsiTransfer.Logging;
using NsiTransfer.Presentation.BackgroundServices;
using Serilog;

namespace NsiTransfer.Extensions;

public static class WebApplicationBuilderExtensions
{
    public static WebApplicationBuilder AddLoggingWithSerilog(this WebApplicationBuilder builder)
    {
        var logConfig = builder.Configuration.GetValidated<LoggingConfig>(nameof(LoggingConfig));
        var logPath = GetLogsPath();

        ConfigureLogging(logConfig.UseFileLogging, logPath, builder.Environment.IsDevelopment());

        Log.Information("Использовать файловое логгирование: {UseFileLogging}", logConfig.UseFileLogging);
 
        if (logConfig.UseFileLogging && logConfig.LogsMaxFolderSizeBytes > 0)
        {
            builder.Services.AddHostedService(serviceProvider =>
                new LogFolderCleanupService(
                    logPath,
                    logConfig.LogsMaxFolderSizeBytes,
                    TimeSpan.FromSeconds(Math.Max(10, logConfig.LogsCleanupIntervalSeconds)),
                    serviceProvider.GetRequiredService<ILogger<LogFolderCleanupService>>()));

            Log.Information("Включена автоочистка логов. Лимит папки: {MaxLogFolderSizeBytes} байт, интервал проверки: {CleanupIntervalSeconds} сек.", logConfig.LogsMaxFolderSizeBytes, Math.Max(10, logConfig.LogsCleanupIntervalSeconds));
        }
        else if (logConfig.UseFileLogging)
        {
            Log.Information("Автоочистка логов отключена. Установите LOGS_MAX_FOLDER_SIZE_BYTES > 0 для включения.");
        }

        builder.Host.UseSerilog();

        return builder;
    }

    private static string GetLogsPath()
    {
        // Путь к папке логов (соседняя директория)
        var logsPath = Path.Combine(Directory.GetCurrentDirectory(), "Logs");
        Directory.CreateDirectory(logsPath);
        return logsPath;
    }

    private static void ConfigureLogging(bool useFileLogging, string logsPath, bool isDevelopment)
    {
        // Настройка Serilog: консоль + файл с разделением по дням
        var config = new LoggerConfiguration()
            .MinimumLevel.Debug()
            .MinimumLevel.Override("Microsoft", Serilog.Events.LogEventLevel.Warning)
            .MinimumLevel.Override("Microsoft.AspNetCore", Serilog.Events.LogEventLevel.Warning);

        if (!isDevelopment)
        {
            // В не-Development режимах логи EF Core будут только Warning и выше
            config.MinimumLevel.Override("Microsoft.EntityFrameworkCore", Serilog.Events.LogEventLevel.Warning);
        }
        config.Enrich.FromLogContext()
            .Enrich.With<ShortSourceContextEnricher>()
            .WriteTo.Console(
                outputTemplate: "[{Timestamp:yyyy-MM-dd HH:mm:ss} {Level:u3}] {ShortSourceContext} - {Message:lj}{NewLine}{Exception}");

        if (useFileLogging)
        {
            config.WriteTo.File(
                path: Path.Combine(logsPath, "log-.txt"),
                rollingInterval: RollingInterval.Day,
                outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} [{Level:u3}] {ShortSourceContext} - {Message:lj}{NewLine}{Exception}",
                retainedFileCountLimit: 62);
        }

        Log.Logger = config.CreateLogger();
    }





    public static WebApplicationBuilder BindConfigModels(this WebApplicationBuilder builder)
    {
        builder.Services.AddSingleton<IValidateOptions<AppConfiguration>, AppConfigurationValidator>();

        // Загружаем configuration.json. Путь можно переопределить переменной окружения CONFIG_FILE_PATH
        // (используется в Docker, где смонтирована директория — атомарная замена файла через rename
        // работает только внутри смонтированной директории, но не поверх single-file mount).
        var configurationPath = Environment.GetEnvironmentVariable("CONFIG_FILE_PATH")
            ?? Path.Combine(AppContext.BaseDirectory, "configuration.json");

        builder.Configuration.AddJsonFile(
            configurationPath,
            optional: false,
            reloadOnChange: true);



        builder.Services.AddOptions<AppConfiguration>()
            .BindConfiguration(nameof(AppConfiguration))
            .ValidateDataAnnotations()
            .ValidateOnStart();

        builder.Services.AddOptions<TargetReferenceNode>()
            .BindConfiguration($"{nameof(AppConfiguration)}:{nameof(AppConfiguration.TargetReferenceNode)}")
            .ValidateDataAnnotations()
            .ValidateOnStart();

        builder.Services.AddOptions<RabbitMqQueues>()
            .BindConfiguration($"{nameof(AppConfiguration)}:{nameof(AppConfiguration.RabbitMqQueues)}")
            .ValidateDataAnnotations()
            .ValidateOnStart();

        builder.Services.AddOptions<RabbitMqRetryParams>()
            .BindConfiguration($"{nameof(AppConfiguration)}:{nameof(AppConfiguration.RabbitMqRetryParams)}")
            .ValidateDataAnnotations()
            .ValidateOnStart();

        builder.Services.AddOptions<PolynomApiSyncOptions>()
            .BindConfiguration($"{nameof(AppConfiguration)}:{nameof(AppConfiguration.PolynomApiSyncOptions)}")
            .ValidateDataAnnotations()
            .ValidateOnStart();

        builder.Services.AddOptions<EmailNotificationsOptions>()
            .BindConfiguration($"{nameof(AppConfiguration)}:{nameof(AppConfiguration.EmailNotifications)}")
            .ValidateDataAnnotations()
            .ValidateOnStart();






        // Будет браться из переменных окружения
        builder.Services.AddOptions<SmtpSettings>()
            .BindConfiguration(nameof(SmtpSettings))
            .ValidateDataAnnotations()
            .ValidateOnStart();

        // Будет браться из переменных окружения
        builder.Services.AddOptions<RabbitMqCreds>()
            .BindConfiguration(nameof(RabbitMqCreds))
            .ValidateDataAnnotations()
            .ValidateOnStart();

        // Будет браться из переменных окружения
        builder.Services.AddOptions<PolynomAuthCreds>()
            .BindConfiguration(nameof(PolynomAuthCreds))
            .ValidateDataAnnotations()
            .ValidateOnStart();

        builder.Services.AddOptions<PolynomConfig>()
            .BindConfiguration($"{nameof(PolynomConfig)}")
            .ValidateDataAnnotations()
            .ValidateOnStart();

        return builder;
    }
}
