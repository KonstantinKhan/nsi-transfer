using NsiTransfer.BLL.Interfaces.Services;
using NsiTransfer.Contract.ConfigModels;
using NsiTransfer.Contract.Exceptions;
using NsiTransfer.Contract.Interfaces;

namespace NsiTransfer.Presentation.Controllers.MinimalApis;

public static class ConfigurationEndpoints
{
    public static void MapConfigurationEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/configuration").WithTags("Configuration");

        group.MapGet("/", (IOptionsHelper<AppConfiguration> optionsHelper) =>
            GetSection(optionsHelper, c => c));

        group.MapPut("/", async (AppConfiguration updated, IConfigurationWriter writer, CancellationToken ct) =>
            await ExecuteWriteAsync(() => writer.ReplaceAsync(updated, ct)));

        group.MapGet("/target-reference-node", (IOptionsHelper<AppConfiguration> optionsHelper) =>
            GetSection(optionsHelper, c => c.TargetReferenceNode));

        group.MapPut("/target-reference-node", async (
            TargetReferenceNode updated, IConfigurationWriter writer, CancellationToken ct) =>
            await ExecuteWriteAsync(() => writer.UpdateAsync(c => c.TargetReferenceNode = updated, ct)));

        group.MapGet("/rabbitmq-queues", (IOptionsHelper<AppConfiguration> optionsHelper) =>
            GetSection(optionsHelper, c => c.RabbitMqQueues));

        group.MapPut("/rabbitmq-queues", async (
            RabbitMqQueues updated, IConfigurationWriter writer, CancellationToken ct) =>
            await ExecuteWriteAsync(() => writer.UpdateAsync(c => c.RabbitMqQueues = updated, ct)));

        group.MapGet("/rabbitmq-retry-params", (IOptionsHelper<AppConfiguration> optionsHelper) =>
            GetSection(optionsHelper, c => c.RabbitMqRetryParams));

        group.MapPut("/rabbitmq-retry-params", async (
            RabbitMqRetryParams updated, IConfigurationWriter writer, CancellationToken ct) =>
            await ExecuteWriteAsync(() => writer.UpdateAsync(c => c.RabbitMqRetryParams = updated, ct)));

        //group.MapGet("/polynom-config", (IOptionsHelper<AppConfiguration> optionsHelper) =>
        //    GetSection(optionsHelper, c => c.PolynomConfig));

        //group.MapPut("/polynom-config", async (
        //    PolynomConfig updated, IConfigurationWriter writer, CancellationToken ct) =>
        //    await ExecuteWriteAsync(() => writer.UpdateAsync(c => c.PolynomConfig = updated, ct)));

        group.MapGet("/polynom-api-sync-options", (IOptionsHelper<AppConfiguration> optionsHelper) =>
            GetSection(optionsHelper, c => c.PolynomApiSyncOptions));

        group.MapPut("/polynom-api-sync-options", async (
            PolynomApiSyncOptions updated, IConfigurationWriter writer, CancellationToken ct) =>
            await ExecuteWriteAsync(() => writer.UpdateAsync(c => c.PolynomApiSyncOptions = updated, ct)));

        group.MapGet("/email-notifications", (IOptionsHelper<AppConfiguration> optionsHelper) =>
            GetSection(optionsHelper, c => c.EmailNotifications));

        group.MapPut("/email-notifications", async (
            EmailNotificationsOptions updated, IConfigurationWriter writer, CancellationToken ct) =>
            await ExecuteWriteAsync(() => writer.UpdateAsync(c => c.EmailNotifications = updated, ct)));
    }

    private static IResult GetSection<TSection>(
        IOptionsHelper<AppConfiguration> optionsHelper,
        Func<AppConfiguration, TSection> selector)
    {
        if (optionsHelper.TryGetCurrentValue(out var config))
        {
            return Results.Ok(selector(config!));
        }

        return ConfigUnavailable();
    }

    private static async Task<IResult> ExecuteWriteAsync(Func<Task> write)
    {
        try
        {
            await write();
            return Results.NoContent();
        }
        catch (ConfigurationValidationException ex)
        {
            return Results.ValidationProblem(new Dictionary<string, string[]>
            {
                [nameof(AppConfiguration)] = ex.Failures.ToArray()
            });
        }
    }

    private static IResult ConfigUnavailable()
        => Results.Problem(
            title: "Конфигурация недоступна",
            detail: "Файл конфигурации на диске содержит невалидные данные.",
            statusCode: StatusCodes.Status503ServiceUnavailable);
}
