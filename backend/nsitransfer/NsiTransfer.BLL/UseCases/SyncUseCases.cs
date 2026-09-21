using Ascon.Polynom.Web.Api.Data.Models.Base;
using Ascon.Polynom.Web.Api.Data.Models.Search;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NsiTransfer.BLL.Interfaces.Services;
using NsiTransfer.BLL.Interfaces.UseCases;
using NsiTransfer.BLL.Services;
using NsiTransfer.BLL.Tools;
using NsiTransfer.Contract;
using NsiTransfer.Contract.ConfigModels;
using NsiTransfer.Contract.Models;
using NsiTransfer.Contract.Models.Ascon;
using NsiTransfer.Contract.Models.Common;
using NsiTransfer.Contract.Models.DTO;
using NsiTransfer.Contract.Models.Enums;
using NsiTransfer.Contract.Models.EventArgs;
using NsiTransfer.DAL.Db.Entities;
using NsiTransfer.DAL.Interfaces.Db;
using NsiTransfer.DAL.Interfaces.MessageBrokers;
using System.Linq.Expressions;
using System.Net.Mime;
using System.Text.Encodings.Web;
using System.Text.Json;

namespace NsiTransfer.BLL.UseCases;

internal class SyncUseCases : ISyncUseCases
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IPolynomApiService _polynomApiService;
    private readonly IRabbitMqPublisher _rabbitMqPublisher;
    private readonly IBackgroundTaskQueue _syncTaskQueue;
    private readonly ISyncNotifier _syncNotifier;
    private readonly IErrorNotifier _errorNotifier;
    private readonly IClassificationCodeProcessor _classificationCodeProcessor;
    private readonly IOptionsMonitor<RabbitMqQueues> _rabbitMqQueues;
    private readonly IOptionsMonitor<Contract.ConfigModels.TargetReferenceNode> _targetRefNode;
    private readonly IOptionsMonitor<PolynomApiSyncOptions> _apiSyncOptions;
    private readonly ILogger<SyncUseCases> _logger;

    private static readonly SemaphoreSlim _createSendingLock = new(1, 1);

    private static readonly JsonSerializerOptions ExceptionJsonOptions = new()
    {
        WriteIndented = true,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
    };

    private static readonly JsonSerializerOptions MessageJsonOptions = new()
    {
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
    };

    public SyncUseCases(
        IUnitOfWork unitOfWork,
        IPolynomApiService polynomApiService,
        IRabbitMqPublisher rabbitMqPublisher,
        IEnumerable<IBackgroundTaskQueue> backgroundTaskQueues,
        ISyncNotifier syncNotifier,
        IErrorNotifier errorNotifier,
        IClassificationCodeProcessor classificationCodeProcessor,
        IOptionsMonitor<RabbitMqQueues> rabbitMqQueues,
        IOptionsMonitor<Contract.ConfigModels.TargetReferenceNode> targetRefNode,
        IOptionsMonitor<PolynomApiSyncOptions> apiSyncOptions,
        ILogger<SyncUseCases> logger)
    {
        _unitOfWork = unitOfWork;
        _polynomApiService = polynomApiService;
        _rabbitMqPublisher = rabbitMqPublisher;
        _syncTaskQueue = backgroundTaskQueues.FirstOrDefault(q => q.QueueName.Equals("sync", StringComparison.OrdinalIgnoreCase))
            ?? throw new ArgumentException("Service for queue of type 'sync' not found.");
        _syncNotifier = syncNotifier;
        _errorNotifier = errorNotifier;
        _classificationCodeProcessor = classificationCodeProcessor;
        _rabbitMqQueues = rabbitMqQueues;
        _targetRefNode = targetRefNode;
        _apiSyncOptions = apiSyncOptions;
        _logger = logger;
    }

    // ═══════════════════════════════════════════════════════════════════════════
    //  Публичные методы (без изменений по логике)
    // ═══════════════════════════════════════════════════════════════════════════

    public async Task<Result> IsThereAlreadyActiveSync(CancellationToken cancellationToken = default)
    {
        var hasActiveSending = await _unitOfWork.Sendings.AnyAsync(s => s.ActiveMarker == true, cancellationToken);

        if (hasActiveSending)
        {
            return "Синхронизация уже запущена. Дождитесь завершения текущего процесса или проверьте статус в базе данных.";
        }

        return Result.Success();
    }

    public async Task<IReadOnlyList<SendingModel>> GetAllSendingsAsync(CancellationToken cancellationToken = default)
    {
        var sendings = await _unitOfWork.Sendings.GetAllAsync(
            selector: s => new SendingModel
            {
                Id = s.Id,
                InitiatedAt = s.InitiatedAt,
                EndedAt = s.EndedAt ?? default,
                InitiatorName = s.InitiatorName,
                StatusId = s.StatusId,
                Messages = s.Messages.Select(m => new MessageModel
                {
                    Id = m.Id,
                    StartedCollectionFromPolynomAt = m.StartedCollectionFromPolynomAt,
                    FinishedCollectionFromPolynomAt = m.FinishedCollectionFromPolynomAt,
                    SentAtQueue = m.SentAtQueue,
                    PolynomObjectsAmountInMessage = m.PolynomObjects != null ? m.PolynomObjects.Count : 0,
                    PublishingResultId = m.PublishingResultId,
                    MessageFailure = m.MessageFailure != null
                        ? new MessageFailureModel
                        {
                            MessageId = m.MessageFailure.MessageId,
                            FailedAt = m.MessageFailure.FailedAt,
                            FailureDescription = m.MessageFailure.FailureDescription,
                            FailureReasonTitle = m.MessageFailure.FailureReasonTitle
                        }
                        : null
                }).ToList()
            },
            orderBy: q => q.OrderByDescending(s => s.InitiatedAt),
            cancellationToken: cancellationToken);

        return sendings;
    }

    public async Task<(IReadOnlyList<SendingModel> Items, bool HasMore)> GetSendingsPageAsync(
        int pageSize, SendingsCursor? cursor, CancellationToken cancellationToken)
    {
        Expression<Func<Sending, bool>>? predicate = null;
        if (cursor is not null)
        {
            var c = cursor.Value;
            predicate = s => s.InitiatedAt < c.InitiatedAt
                        || (s.InitiatedAt == c.InitiatedAt && s.Id.CompareTo(c.Id) < 0);
        }

        var sendings = await _unitOfWork.Sendings.GetAllAsync(
            selector: s => new SendingModel
            {
                Id = s.Id,
                InitiatedAt = s.InitiatedAt,
                EndedAt = s.EndedAt ?? default,
                InitiatorName = s.InitiatorName,
                StatusId = s.StatusId,
                TargetReferenceNodeName = s.TargetReferenceNode.Name,
                Messages = s.Messages.Select(m => new MessageModel
                {
                    Id = m.Id,
                    StartedCollectionFromPolynomAt = m.StartedCollectionFromPolynomAt,
                    FinishedCollectionFromPolynomAt = m.FinishedCollectionFromPolynomAt,
                    SentAtQueue = m.SentAtQueue,
                    PolynomObjectsAmountInMessage = m.PolynomObjects != null ? m.PolynomObjects.Count : 0,
                    PublishingResultId = m.PublishingResultId,
                    MessageType = m.MessageType,
                    MessageFailure = m.MessageFailure != null
                        ? new MessageFailureModel
                        {
                            MessageId = m.MessageFailure.MessageId,
                            FailedAt = m.MessageFailure.FailedAt,
                            FailureDescription = m.MessageFailure.FailureDescription,
                            FailureReasonTitle = m.MessageFailure.FailureReasonTitle
                        }
                        : null
                }).ToList()
            },
            predicate: predicate,
            orderBy: q => q.OrderByDescending(s => s.InitiatedAt).ThenByDescending(s => s.Id),
            take: pageSize + 1,
            cancellationToken: cancellationToken);

        var hasMore = sendings.Count > pageSize;
        var items = hasMore ? sendings.Take(pageSize).ToList() : sendings;

        return (items, hasMore);
    }

    public async Task<SendingModel?> GetSending(Guid sendingId, CancellationToken cancellationToken = default)
    {
        var sending = await _unitOfWork.Sendings.GetAsync(
            selector: s => new SendingModel
            {
                Id = s.Id,
                InitiatedAt = s.InitiatedAt,
                EndedAt = s.EndedAt ?? default,
                InitiatorName = s.InitiatorName,
                StatusId = s.StatusId,
                Messages = s.Messages.Select(m => new MessageModel
                {
                    Id = m.Id,
                    StartedCollectionFromPolynomAt = m.StartedCollectionFromPolynomAt,
                    FinishedCollectionFromPolynomAt = m.FinishedCollectionFromPolynomAt,
                    SentAtQueue = m.SentAtQueue,
                    PolynomObjectsAmountInMessage = m.PolynomObjects != null ? m.PolynomObjects.Count : 0,
                    PublishingResultId = m.PublishingResultId,
                    MessageFailure = m.MessageFailure != null
                        ? new MessageFailureModel
                        {
                            MessageId = m.MessageFailure.MessageId,
                            FailedAt = m.MessageFailure.FailedAt,
                            FailureDescription = m.MessageFailure.FailureDescription,
                            FailureReasonTitle = m.MessageFailure.FailureReasonTitle
                        }
                        : null
                }).ToList()
            },
            singleOrFirst: SingleOrFirst.First,
            predicate: s => s.Id == sendingId,
            cancellationToken: cancellationToken);

        return sending;
    }

    // ═══════════════════════════════════════════════════════════════════════════
    //  Запуск синхронизации
    // ═══════════════════════════════════════════════════════════════════════════

    public async Task<(Result Result, SendingModel? Sending)> StartDataCollectionAndWaitAsync(
        string initiatorName, CancellationToken cancellationToken = default)
    {
        var sendingResult = await CreateSendingIfNoneActiveAsync(initiatorName, cancellationToken);
        if (!sendingResult.IsSuccess)
            return (sendingResult.ErrorMessage, null);

        var sendingId = sendingResult.Data!.Id;
        _syncNotifier.InitializeStream(sendingId);

        try
        {
            var collectionResult = await ContinueDataCollectionAsync(sendingId, cancellationToken);
            var sendingModel = MapToModel(sendingResult.Data!);

            return collectionResult.IsSuccess
                ? (Result.Success(), sendingModel)
                : (collectionResult, sendingModel);
        }
        catch (Exception ex)
        {
            await HandleSyncError(sendingId, ex, _unitOfWork, _logger, _syncNotifier, false, cancellationToken);
            return (ex.Message, MapToModel(sendingResult.Data!));
        }
        finally
        {
            _syncNotifier.Complete(sendingId);
        }
    }

    public async Task<(Result Result, SendingModel? Sending)> StartDataCollectionInBackgroundAsync(
        string initiatorName, CancellationToken cancellationToken = default)
    {
        var sendingResult = await CreateSendingIfNoneActiveAsync(initiatorName, cancellationToken);
        if (!sendingResult.IsSuccess)
            return (sendingResult.ErrorMessage, null);

        var sendingId = sendingResult.Data!.Id;
        _syncNotifier.InitializeStream(sendingId);

        await _syncTaskQueue.QueueBackgroundWorkItem(async (sp, ct) =>
        {
            using var scope = sp.CreateScope();
            var scopedUseCases = scope.ServiceProvider.GetRequiredService<ISyncUseCases>();

            try
            {
                await scopedUseCases.ContinueDataCollectionAsync(sendingId, ct);
            }
            catch (Exception ex)
            {
                var scopedLogger = scope.ServiceProvider.GetRequiredService<ILogger<BackgroundService>>();
                var scopedUnitOfWork = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
                await HandleSyncError(sendingId, ex, scopedUnitOfWork, scopedLogger, _syncNotifier, true, cancellationToken);
            }
            finally
            {
                _syncNotifier.Complete(sendingId);
            }
        });

        return (Result.Success(), MapToModel(sendingResult.Data!));
    }

    public async Task<Result> RebuildGroupCodeCacheInBackgroundAsync(CancellationToken cancellationToken = default)
    {
        var hasActiveSync = await IsThereAlreadyActiveSync(cancellationToken);
        if (!hasActiveSync.IsSuccess)
        {
            return $"Нельзя запустить переиндексацию кеша кодов классификатора во время выполнения синхронизации: {hasActiveSync.ErrorMessage}";
        }

        await _syncTaskQueue.QueueBackgroundWorkItem(async (sp, ct) =>
        {
            using var scope = sp.CreateScope();
            var scopedProcessor = scope.ServiceProvider.GetRequiredService<IClassificationCodeProcessor>();
            var scopedLogger = scope.ServiceProvider.GetRequiredService<ILogger<SyncUseCases>>();

            var rebuildResult = await scopedProcessor.RebuildGroupCodeCacheAsync(ct);

            if (!rebuildResult.IsSuccess)
            {
                scopedLogger.LogError("Переиндексация кеша кодов классификатора завершилась ошибкой: {Error}", rebuildResult.ErrorMessage);
                return;
            }

            scopedLogger.LogInformation(
                "Переиндексация кеша кодов классификатора завершена. Проиндексировано: {Indexed}, пропущено: {Skipped}, с ошибками: {Errors}",
                rebuildResult.Data!.GroupsIndexed, rebuildResult.Data.GroupsSkippedNotAClassificationGroup, rebuildResult.Data.GroupsWithErrors);

            if (rebuildResult.Data.Errors.Count > 0)
            {
                scopedLogger.LogWarning("Ошибки при переиндексации кеша кодов классификатора: {Errors}", string.Join("; ", rebuildResult.Data.Errors));
            }
        });

        return Result.Success();
    }

    /// <summary>
    /// Добавляет каждый объект отправления отдельной строкой в MessageObjects (нормализованная копия элементов
    /// массива Message.SerializedMessage) — решает проблему работы с большими отправлениями в pgAdmin и поиска
    /// конкретного объекта по json-запросу. Только для новых сообщений, без миграции ранее накопленных данных.
    /// Не влияет на публикацию в RabbitMQ — источник для брокера остаётся Message.SerializedMessage.
    /// </summary>
    private async Task AddMessageObjectsAsync(Message message, List<PolynomObjectWithShortProperties> objects, CancellationToken cancellationToken)
    {
        foreach (var obj in objects)
        {
            await _unitOfWork.MessageObjects.AddAsync(new MessageObject
            {
                Message = message,
                PolynomObjectId = obj.ObjectId,
                PolynomTypeId = obj.TypeId,
                Name = obj.Name,
                SerializedObject = JsonSerializer.Serialize(obj, MessageJsonOptions)
            }, cancellationToken);
        }
    }

    // ═══════════════════════════════════════════════════════════════════════════
    //  Создание Sending и подготовка периода
    // ═══════════════════════════════════════════════════════════════════════════

    private async Task<Result<Sending>> CreateSendingIfNoneActiveAsync(string initiatorName, CancellationToken cancellationToken = default)
    {
        await _createSendingLock.WaitAsync(cancellationToken);
        try
        {
            var refNode = await _unitOfWork.TargetReferenceNodes.FirstOrDefaultAsync(
                predicate: n => n.ObjectId == _targetRefNode.CurrentValue.TargetReferenceNodeObjectId
                            && n.TypeId == _targetRefNode.CurrentValue.TargetReferenceNodeTypeId,
                cancellationToken: cancellationToken);

            if (refNode == null)
                return "Не удалось найти запись в БД об идентификаторах справочника Полинома, в котором должен осуществляться поиск объектов.";

            var sending = new Sending
            {
                Id = Guid.NewGuid(),
                InitiatedAt = DateTime.UtcNow,
                InitiatorName = initiatorName,
                TargetReferenceNode = refNode
            };
            sending.SetStatus(SendingStatusEnum.Initiated);

            try
            {
                await _unitOfWork.Sendings.AddAsync(sending, cancellationToken);
                await _unitOfWork.SaveChangesAsync(cancellationToken);
            }
            catch (DbUpdateException ex)
            {
                var stillActive = await _unitOfWork.Sendings.AnyAsync(
                    s => (s.StatusId == SendingStatusEnum.Pending || s.StatusId == SendingStatusEnum.Initiated)
                        && (s.ActiveMarker ?? false),
                    cancellationToken);

                if (stillActive)
                    return "Синхронизация уже запущена. Дождитесь завершения текущего процесса или проверьте статус в базе данных.";

                var err = "Ошибка в процессе создания сущности Sending в БД. Необходимо проверить доступность БД.";
                _logger.LogError(ex, err);
                return $"{err}\n{ex.GetAllMessages()}";
            }

            return sending;
        }
        finally
        {
            _createSendingLock.Release();
        }
    }

    public async Task<Result> ContinueDataCollectionAsync(Guid sendingId, CancellationToken cancellationToken)
    {
        var sending = await _unitOfWork.Sendings.FirstOrDefaultAsync(s => s.Id == sendingId, cancellationToken: cancellationToken);

        if (sending == null)
            return $"Не удалось получить из БД сущность Sending с SendingId '{sendingId}' при попытке продолжить синхронизацию.";

        return await GetSearchDateTimePeriod(sending, cancellationToken)
            .BindAsync(timePeriod => PendingSending(sending, timePeriod, cancellationToken));
    }

    private async Task<Result<DateTimePeriod>> GetSearchDateTimePeriod(Sending sending, CancellationToken cancellationToken)
    {
        DateTime lastCollectionStartedAt;

        try
        {
            var lastCompletedSending = await _unitOfWork.Sendings.FirstOrDefaultAsync(
                predicate: s => s.StatusId == SendingStatusEnum.Completed
                    && s.TargetReferenceNode.ObjectId == _targetRefNode.CurrentValue.TargetReferenceNodeObjectId
                    && s.TargetReferenceNode.TypeId == _targetRefNode.CurrentValue.TargetReferenceNodeTypeId,
                orderBy: sq => sq.OrderByDescending(s => s.EndedAt),
                cancellationToken: cancellationToken);

            // Первый sync для этого TargetReferenceNode (ни одного Completed Sending ещё не было) — стартуем
            // период с текущего момента, а не с DateTime.MinValue. Иначе первый запуск воспринимает ВСЮ историю
            // изменений справочника как "diff за период" и пытается отправить в брокер всё хранилище целиком.
            // Если реально нужен полный первичный бэкфилл — см. docs/wiki/sync-flow.md, раздел про первый запуск.
            var rawTime = lastCompletedSending?.EndedAt ?? DateTime.UtcNow;
            lastCollectionStartedAt = rawTime.AddTicks((TimeSpan.TicksPerSecond - (rawTime.Ticks % TimeSpan.TicksPerSecond)) % TimeSpan.TicksPerSecond);
        }
        catch (Exception ex)
        {
            var err = $"Ошибка при подготовке отправления (Id: {sending.Id}).";
            _logger.LogError(ex, err);

            sending.SetStatus(SendingStatusEnum.ErrorOccuredWhilePreparing);
            await _unitOfWork.SaveChangesAsync(cancellationToken);
            await _syncNotifier.NotifyAsync(sending.Id, new SyncEvent("StatusChanged", new { Status = sending.StatusId.ToString() }), cancellationToken);
            return $"{err} \n{ex.Message}";
        }

        var upperTimeEdge = DateTime.UtcNow.AddMinutes(1);
        return new DateTimePeriod(lastCollectionStartedAt, upperTimeEdge);
    }

    private async Task<Result> PendingSending(Sending sending, DateTimePeriod timePeriod, CancellationToken cancellationToken)
    {
        try
        {
            sending.SetStatus(SendingStatusEnum.Pending);
            await _unitOfWork.SaveChangesAsync(cancellationToken);
            await _syncNotifier.NotifyAsync(sending.Id, new SyncEvent("StatusChanged", new
            {
                Status = sending.StatusId.ToString(),
                SendingModel = MapToModel(sending)
            }), cancellationToken);

            var collectionOutcome = await CollectObjectsAndSend(sending, timePeriod.Start, timePeriod.End, cancellationToken);

            // ── РЕТРАЙ ОБЪЕКТОВ ИЗ ПРЕДЫДУЩИХ ОШИБОК ──────────────────────────
            // Запускается после основного цикла, если синхронизация не упала с критической ошибкой
            if (collectionOutcome != CollectionOutcom.Failed)
            {
                var retryOutcome = await RetryFailedObjectsAsync(sending, cancellationToken);
                if (retryOutcome == CollectionOutcom.Failed)
                {
                    collectionOutcome = CollectionOutcom.Failed;
                }
                else if (collectionOutcome == CollectionOutcom.NoDataFound && retryOutcome == CollectionOutcom.Completed)
                {
                    collectionOutcome = CollectionOutcom.Completed;
                }
            }

            switch (collectionOutcome)
            {
                case CollectionOutcom.Completed:
                    sending.SetStatus(SendingStatusEnum.Completed);
                    await _unitOfWork.SaveChangesAsync(cancellationToken);
                    await _syncNotifier.NotifyAsync(sending.Id, new SyncEvent("SendingCompleted", new 
                    { 
                        sending.EndedAt 
                    }), cancellationToken);

                    return Result.Success();

                case CollectionOutcom.NoDataFound:
                    sending.SetStatus(SendingStatusEnum.EmptySending);
                    await _unitOfWork.SaveChangesAsync(cancellationToken);
                    await _syncNotifier.NotifyAsync(sending.Id, new SyncEvent("StatusChanged", new
                    {
                        Status = sending.StatusId.ToString(),
                        sending.EndedAt
                    }), cancellationToken);

                    return Result.Success();

                case CollectionOutcom.Failed:
                    if (sending.StatusId is SendingStatusEnum.Pending or SendingStatusEnum.Initiated)
                    {
                        sending.SetStatus(SendingStatusEnum.ErrorUnknown);
                    }

                    await _unitOfWork.SaveChangesAsync(cancellationToken);
                    await _syncNotifier.NotifyAsync(sending.Id, new SyncEvent("StatusChanged", new
                    {
                        Status = sending.StatusId.ToString(),
                        sending.EndedAt,
                        Error = "Процесс синхронизации прерван из-за ошибки"
                    }), cancellationToken);

                    return "Не удалось отправить всё отправление в брокер сообщений. Подробности в логах в БД.";

                default:
                    throw new NotImplementedException();
            }
        }
        catch (Exception ex)
        {
            var err = "Неизвестная критическая ошибка при синхронизации данных.";
            _logger.LogError(ex, err);
            sending.SetStatus(SendingStatusEnum.ErrorUnknown);
            await _unitOfWork.SaveChangesAsync(cancellationToken);
            await _syncNotifier.NotifyAsync(sending.Id, new SyncEvent("StatusChanged", new
            {
                Status = sending.StatusId.ToString(),
                Error = err
            }), cancellationToken);
            return $"{err} \n{ex.Message}";
        }
    }

    // ═══════════════════════════════════════════════════════════════════════════
    //  Сбор объектов и отправка
    // ═══════════════════════════════════════════════════════════════════════════

    private async Task<CollectionOutcom> CollectObjectsAndSend(
        Sending sending,
        DateTime startTime,
        DateTime endTime,
        CancellationToken cancellationToken)
    {
        PaginatedList<PropertySearchResultObject> foundObjects;
        var pageNumber = 0;

        do
        {
            pageNumber++;

            // Создаём Message для текущей страницы
            var currentMessage = new Message
            {
                StartedCollectionFromPolynomAt = DateTime.UtcNow,
                SendingId = sending.Id,
                MessageType = MessageTypeEnum.FirstSend
            };
            var test = await _unitOfWork.Messages.AddAsync(currentMessage, cancellationToken);
            await _unitOfWork.SaveChangesAsync(cancellationToken);

            await _syncNotifier.NotifyAsync(sending.Id, new SyncEvent("MessageCreated", new
            {
                currentMessage.Id,
                currentMessage.StartedCollectionFromPolynomAt,
                PageNumber = pageNumber
            }), cancellationToken);

            // Запрос объектов из Полином
            var foundObjectsResult = await _polynomApiService.GetDiffsInTimePeriod(startTime, endTime, pageNumber, 100, cancellationToken);

            if (!foundObjectsResult.IsSuccess)
            {
                return await FailMessageCollectionAsync(
                    sending, currentMessage,
                    "Ошибка поиска объектов в Полином при подготовке отправления в брокер очередей",
                    foundObjectsResult.ErrorMessage!,
                    foundObjectsResult,
                    cancellationToken);
            }

            foundObjects = foundObjectsResult.Data!;

            // Нет данных, удаляем пустое сообщение
            if (pageNumber == 1 && (foundObjects.Items == null || foundObjects.Items.Count == 0))
            {
                _unitOfWork.Messages.Delete(currentMessage);
                await _unitOfWork.SaveChangesAsync(cancellationToken);
                await _syncNotifier.NotifyAsync(sending.Id, new SyncEvent("MessageEmpty", new { currentMessage.Id }), cancellationToken);
                return CollectionOutcom.NoDataFound;
            }

            // Сбор свойств и классификационных кодов
            var objectsWithProperties = new List<PolynomObjectWithShortProperties>(foundObjects.Items.Count);
            var objectsLogs = new List<DAL.Db.Entities.PolynomObject>(foundObjects.Items.Count);

            foreach (var obj in foundObjects.Items)
            {
                Console.WriteLine($"Найденный объект: {obj.Name}");
                var propsResult = await _polynomApiService
                    .GetAllPropertiesOfObject(obj.ObjectId, obj.TypeId, cancellationToken)
                    .OnFailureAsync(res => res.AddError(
                        $"Не удалось получить свойства объекта \n" +
                        $"ObjectId '{obj.ObjectId}' TypeId '{obj.TypeId}' Name '{obj.Name}';\n" +
                        $"PageNumber: '{pageNumber}'\nПроцесс синхронизации прерван."));

                if (!propsResult.IsSuccess)
                {
                    return await FailMessageCollectionAsync(
                        sending, currentMessage,
                        "Ошибка получения свойств объектов в Полином при подготовке отправления в брокер очередей",
                        propsResult.ErrorMessage!,
                        propsResult,
                        cancellationToken);
                }

                var objWithProps = ModelMapper.CreateObjectWithShortProperties(obj, propsResult.Data!);

                // Если у объекта нет понятия "Данные классификатора" — полностью пропускаем
                // flow по установке кодов классификатора.
                if (!IsConceptNameForClassificationDataExists(propsResult.Data!))
                {
                    // Удаляем старую ошибку, если объект был проблемным раньше
                    var existingFailures = await _unitOfWork.PolynomObjectFailures.GetAllAsync(
                        predicate: f => f.PolynomObjectId == objWithProps.ObjectId && f.PolynomTypeId == objWithProps.TypeId,
                        cancellationToken: cancellationToken);
                    if (existingFailures.Count > 0)
                    {
                        _unitOfWork.PolynomObjectFailures.DeleteRange(existingFailures);
                    }

                    objectsWithProperties.Add(objWithProps);
                    objectsLogs.Add(new DAL.Db.Entities.PolynomObject
                    {
                        Name = objWithProps.Name,
                        PolynomObjectId = objWithProps.ObjectId,
                        PolynomTypeId = objWithProps.TypeId,
                        Message = currentMessage
                    });
                    continue;
                }

                // Обработка кода классификатора (делегирована в IClassificationCodeProcessor).
                var processResult = await _classificationCodeProcessor.ProcessAsync(objWithProps, currentMessage, cancellationToken);

                if (!processResult.IsSuccess)
                {
                    // Объект исключён из текущего batch и не будет отправлен в RabbitMQ.
                    // Ошибка уже залогирована внутри ProcessAsync и сохранена в PolynomObjectFailures.
                    _logger.LogWarning(
                        "Object excluded from batch for message (will not be published to RabbitMQ): " +
                        "ObjectId={ObjectId}, TypeId={TypeId}, Name='{ObjectName}'. Reason: {FailureReason}",
                        objWithProps.ObjectId, objWithProps.TypeId, objWithProps.Name, processResult.ErrorMessage);

                    var errorTitle = $"Classification error for object {objWithProps.ObjectId} ({objWithProps.TypeId}): {objWithProps.Name}";
                    await _errorNotifier.NotifyAboutErrorAsync(processResult.ErrorMessage, errorTitle);

                    continue;
                }

                // Успешно обработали — удаляем старую запись об ошибке из БД, если была
                var existingFailuresForSuccess = await _unitOfWork.PolynomObjectFailures.GetAllAsync(
                    predicate: f => f.PolynomObjectId == objWithProps.ObjectId && f.PolynomTypeId == objWithProps.TypeId,
                    cancellationToken: cancellationToken);
                if (existingFailuresForSuccess.Count > 0)
                {
                    _unitOfWork.PolynomObjectFailures.DeleteRange(existingFailuresForSuccess);
                }

                objectsWithProperties.Add(objWithProps);
                objectsLogs.Add(processResult.Data!);
            }

            // Если после всех фильтров не осталось ни одного объекта для отправки
            if (objectsWithProperties.Count == 0)
            {
                // Проверяем, есть ли ошибки классификации для этого сообщения
                var failureCount = await _unitOfWork.PolynomObjectFailures.CountAsync(
                    f => f.MessageId == currentMessage.Id,
                    cancellationToken);

                if (failureCount > 0)
                {
                    // Сообщение содержит только ошибки — записываем в MessageFailure
                    var messageFailure = new MessageFailure
                    {
                        Message = currentMessage,
                        FailedAt = DateTime.UtcNow,
                        FailureReasonTitle = "Все объекты в сообщении имеют ошибки классификации",
                        FailureDescription = $"Обработано объектов с ошибками: {failureCount}. Объекты не отправлены в очередь."
                    };
                    await _unitOfWork.MessageFailures.AddAsync(messageFailure, cancellationToken);
                    await _unitOfWork.SaveChangesAsync(cancellationToken);
                }
                else
                {
                    // Нет ошибок и нет успешных объектов — сообщение пусто, удаляем
                    _unitOfWork.Messages.Delete(currentMessage);
                    await _unitOfWork.SaveChangesAsync(cancellationToken);
                }

                await _syncNotifier.NotifyAsync(sending.Id, new SyncEvent("MessageEmpty", new { currentMessage.Id }), cancellationToken);

                // Если это была первая и единственная страница данных, значит всё отправление пустое
                if (pageNumber == 1 && !foundObjects.HasNextPage)
                {
                    return CollectionOutcom.NoDataFound;
                }

                // Если есть ещё страницы, просто переходим к следующей.
                // Если это была последняя страница (но не первая), цикл завершится, и метод вернёт Completed, что корректно.
                continue;
            }

            // ── Сериализация и сохранение ────────────────────────────────────
            currentMessage.FinishedCollectionFromPolynomAt = DateTime.UtcNow;
            currentMessage.SerializedMessage = JsonSerializer.Serialize(objectsWithProperties, MessageJsonOptions);
            _logger.LogInformation("[MESSAGE SERIALIZED] Current message preview (first 1500 chars): {MessagePreview}",
                currentMessage.SerializedMessage.Length > 1500 ? currentMessage.SerializedMessage[..1500] : currentMessage.SerializedMessage);
            currentMessage.PolynomObjects = objectsLogs;
            await AddMessageObjectsAsync(currentMessage, objectsWithProperties, cancellationToken);
            await _unitOfWork.SaveChangesAsync(cancellationToken);

            await _syncNotifier.NotifyAsync(sending.Id, new SyncEvent("ObjectsCollected", new
            {
                currentMessage.Id,
                ObjectsCount = objectsLogs.Count,
                currentMessage.FinishedCollectionFromPolynomAt
            }), cancellationToken);

            // ── Публикация в RabbitMQ ────────────────────────────────────────
            var publishingResult = await PublishMessage(currentMessage, cancellationToken);

            if (!publishingResult.IsSuccess)
            {
                return await FailMessagePublishingAsync(
                    sending, currentMessage,
                    $"Не удалось опубликовать отправление с объектами Полином в брокер сообщений. MessageId: {currentMessage.Id}.",
                    publishingResult.PossibleException,
                    cancellationToken);
            }

            await _unitOfWork.SaveChangesAsync(cancellationToken);

            await _syncNotifier.NotifyAsync(sending.Id, new SyncEvent("MessagePublished", new
            {
                currentMessage.Id,
                currentMessage.SentAtQueue
            }), cancellationToken);

        } while (foundObjects.HasNextPage);

        return CollectionOutcom.Completed;
    }

    // ═══════════════════════════════════════════════════════════════════════════
    //  Ретрай объектов, ранее упавших с ошибкой классификации
    // ═══════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Повторная обработка объектов из PolynomObjectFailures.
    /// Берёт ошибки только из предыдущих Sending, исключает те объекты, что уже успешно отправлены в текущем Sending.
    /// </summary>
    private async Task<CollectionOutcom> RetryFailedObjectsAsync(Sending sending, CancellationToken cancellationToken)
    {
        // 1. Получаем все уникальные пары (PolynomObjectId, PolynomTypeId), которые уже успешно собраны в текущем Sending
        var sentInCurrentSending = await _unitOfWork.PolynomObjects.GetAllAsync(
            selector: po => new { PolynomObjectId = (long)po.PolynomObjectId, po.PolynomTypeId },
            predicate: po => po.Message.SendingId == sending.Id,
            cancellationToken: cancellationToken);

        if (sentInCurrentSending == null) return CollectionOutcom.Completed;

        var sentHashSet = sentInCurrentSending.Select(x => (x.PolynomObjectId, x.PolynomTypeId)).ToHashSet();

        // 2. Получаем ошибки классификации, сгенерированные в предыдущих Sending
        var previousFailures = await _unitOfWork.PolynomObjectFailures.GetAllAsync(
            predicate: f => f.Message.SendingId != sending.Id,
            cancellationToken: cancellationToken);

        // 3. Фильтруем: оставляем только те, которых нет в текущей успешной отправке. Дедуплицируем.
        var objectsToRetry = previousFailures
            .Where(f => !sentHashSet.Contains(((long)f.PolynomObjectId, f.PolynomTypeId)))
            .GroupBy(f => new { f.PolynomObjectId, f.PolynomTypeId })
            .Select(g => g.OrderByDescending(f => f.FailedAt).First())
            .ToList();

        if (objectsToRetry.Count == 0)
        {
            return CollectionOutcom.Completed;
        }

        // 4. Создаём отдельное сообщение для объектов, восстанавливаемых из ретрая
        var retryMessage = new Message
        {
            StartedCollectionFromPolynomAt = DateTime.UtcNow,
            SendingId = sending.Id,
            MessageType = MessageTypeEnum.Retry
        };
        await _unitOfWork.Messages.AddAsync(retryMessage, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        await _syncNotifier.NotifyAsync(sending.Id, new SyncEvent("RetryMessageCreated", new
        {
            retryMessage.Id,
            ObjectsToRetryCount = objectsToRetry.Count
        }), cancellationToken);

        var retryObjectsWithProperties = new List<PolynomObjectWithShortProperties>();
        var retryObjectsLogs = new List<DAL.Db.Entities.PolynomObject>();

        // 5. Заново получаем свойства и прогоняем логику классификации
        foreach (var failure in objectsToRetry)
        {
            var propsResult = await _polynomApiService
                .GetAllPropertiesOfObject(failure.PolynomObjectId, failure.PolynomTypeId, cancellationToken);

            if (!propsResult.IsSuccess)
            {
                // Если объект не найден в Полиноме (404) — удаляем его из ошибок, он больше не существует
                if (propsResult.ErrorMessage?.Contains("Статус: 404") == true)
                {
                    _logger.LogWarning(
                        "Object no longer exists in Polynom (404), deleting failure record: " +
                        "ObjectId={ObjectId}, TypeId={TypeId}, Name='{ObjectName}'",
                        failure.PolynomObjectId, failure.PolynomTypeId, failure.ObjectName);
                    _unitOfWork.PolynomObjectFailures.Delete(failure);
                    await _unitOfWork.SaveChangesAsync(cancellationToken);
                    continue;
                }

                // Иные ошибки API — оставляем запись для повторного ретрая
                _logger.LogWarning(
                    "Failed to get properties for retry object (will retry on next cycle): " +
                    "ObjectId={ObjectId}, TypeId={TypeId}, Error='{Error}'",
                    failure.PolynomObjectId, failure.PolynomTypeId, propsResult.ErrorMessage);
                continue;
            }

            // Если объект вернулся пустой (без контрактов) — это удалённый объект, удаляем его из ошибок
            if (propsResult.Data?.AllContracts == null || propsResult.Data.AllContracts.Count == 0)
            {
                _logger.LogWarning(
                    "Object has no contracts/properties (likely deleted), deleting failure record: " +
                    "ObjectId={ObjectId}, TypeId={TypeId}, Name='{ObjectName}'",
                    failure.PolynomObjectId, failure.PolynomTypeId, failure.ObjectName);
                _unitOfWork.PolynomObjectFailures.Delete(failure);
                await _unitOfWork.SaveChangesAsync(cancellationToken);
                continue;
            }

            var searchResultObj = new PropertySearchResultObject
            {
                ObjectId = failure.PolynomObjectId,
                TypeId = failure.PolynomTypeId,
                Name = failure.ObjectName
            };

            var objWithProps = ModelMapper.CreateObjectWithShortProperties(searchResultObj, propsResult.Data!);

            // Check if object is outside all classifier groups (CanUnassign = true)
            var conceptName = _apiSyncOptions.CurrentValue.ConceptNameForClassificationData;
            var classificationContract = propsResult.Data!.AllContracts
                .Find(c => c.Name.Equals(conceptName, StringComparison.OrdinalIgnoreCase));

            if (classificationContract != null && classificationContract.CanUnassign)
            {
                // Object has classifier concept but is not assigned to any group
                var classifierContractMapped = objWithProps.Contracts
                    .Find(c => c.Name.Equals(conceptName, StringComparison.OrdinalIgnoreCase));
                var codePropName = _apiSyncOptions.CurrentValue.ClassificationCodePropertyName;
                var codeValue = classifierContractMapped?.Properties
                    .Find(p => p.Name.Equals(codePropName, StringComparison.OrdinalIgnoreCase))?.Value;

                if (string.IsNullOrWhiteSpace(codeValue))
                {
                    // No classifier code and outside all groups — delete stale failure record
                    _unitOfWork.PolynomObjectFailures.Delete(failure);
                    continue;
                }
                else
                {
                    // Has classifier code but outside all groups — notify and keep record
                    await _errorNotifier.NotifyAboutErrorAsync(
                        $"Объект '{objWithProps.Name}' (ObjectId: {objWithProps.ObjectId}, TypeId: {objWithProps.TypeId}) " +
                        $"имеет код классификатора '{codeValue}', но не входит ни в одну группу справочника Классификатора " +
                        $"(CanUnassign = true). Запись об ошибке сохранена в PolynomObjectFailures.",
                        "NsiTransfer: объект вне групп справочника Классификатора");
                    continue;
                }
            }

            if (!IsConceptNameForClassificationDataExists(propsResult.Data!))
            {
                // Внезапно у объекта пропало понятие классификатора — считаем валидным и просто отправляем
                retryObjectsWithProperties.Add(objWithProps);
                retryObjectsLogs.Add(new DAL.Db.Entities.PolynomObject
                {
                    Name = objWithProps.Name,
                    PolynomObjectId = objWithProps.ObjectId,
                    PolynomTypeId = objWithProps.TypeId,
                    Message = retryMessage
                });

                // Удаляем старую ошибку, так как объект теперь проходим
                _unitOfWork.PolynomObjectFailures.Delete(failure);
                continue;
            }

            var processResult = await _classificationCodeProcessor.ProcessAsync(objWithProps, retryMessage, cancellationToken);

            if (processResult.IsSuccess)
            {
                // Успешно обработали — удаляем старую запись об ошибке из БД
                _unitOfWork.PolynomObjectFailures.Delete(failure);
                retryObjectsWithProperties.Add(objWithProps);
                retryObjectsLogs.Add(processResult.Data!);
            }
            // else: ClassificationCodeProcessor внутри себя уже удалил старую ошибку и создал новую, привязанную к retryMessage
        }

        // Сохраняем удаления старых ошибок и добавление новых
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        // 6. Если есть что отправлять — формируем сообщение и публикуем
        if (retryObjectsWithProperties.Count > 0)
        {
            retryMessage.FinishedCollectionFromPolynomAt = DateTime.UtcNow;
            retryMessage.SerializedMessage = JsonSerializer.Serialize(retryObjectsWithProperties, MessageJsonOptions);
            _logger.LogInformation("[MESSAGE SERIALIZED] Retry message preview (first 1500 chars): {MessagePreview}",
                retryMessage.SerializedMessage.Length > 1500 ? retryMessage.SerializedMessage[..1500] : retryMessage.SerializedMessage);
            retryMessage.PolynomObjects = retryObjectsLogs;
            await AddMessageObjectsAsync(retryMessage, retryObjectsWithProperties, cancellationToken);
            await _unitOfWork.SaveChangesAsync(cancellationToken);

            await _syncNotifier.NotifyAsync(sending.Id, new SyncEvent("RetryObjectsCollected", new
            {
                retryMessage.Id,
                ObjectsCount = retryObjectsLogs.Count,
                retryMessage.FinishedCollectionFromPolynomAt
            }), cancellationToken);

            var publishingResult = await PublishMessage(retryMessage, cancellationToken);

            if (!publishingResult.IsSuccess)
            {
                return await FailMessagePublishingAsync(
                    sending, retryMessage,
                    $"Не удалось опубликовать отправление с объектами Полином (из ретрая) в брокер сообщений. MessageId: {retryMessage.Id}.",
                    publishingResult.PossibleException,
                    cancellationToken);
            }

            await _unitOfWork.SaveChangesAsync(cancellationToken);

            await _syncNotifier.NotifyAsync(sending.Id, new SyncEvent("RetryMessagePublished", new
            {
                retryMessage.Id,
                retryMessage.SentAtQueue
            }), cancellationToken);
        }
        else
        {
            // Если ни один объект не был успешно восстановлен — отправляем информацию о том, что сообщение-retry получилось пустым
            await _syncNotifier.NotifyAsync(sending.Id, new SyncEvent("RetryMessageEmpty", new 
            { 
                retryMessage.Id,
                FinishiedCollectionFromPolynomAt = DateTime.UtcNow
            }), cancellationToken);
        }

        return CollectionOutcom.Completed;
    }

    // ═══════════════════════════════════════════════════════════════════════════
    //  Единые обработчики ошибок на уровне сообщения
    // ═══════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Единый шаблон: ошибка сбора данных для Message.
    /// Сохраняет MessageFailure, отправляет email, уведомляет SSE, возвращает Failed.
    /// </summary>
    private async Task<CollectionOutcom> FailMessageCollectionAsync<T>(
        Sending sending,
        Message message,
        string failureTitle,
        string failureDescription,
        Result<T> result,
        CancellationToken cancellationToken)
    {
        await HandleErrorOccuredWhileCollection(sending, message, failureTitle, failureDescription, cancellationToken);
        await _errorNotifier.NotifyAboutErrorAsync(result, failureTitle);

        await NotifyMessageFailed(sending, message, failureDescription, cancellationToken);

        return CollectionOutcom.Failed;
    }

    /// <summary>
    /// Единый шаблон: ошибка публикации Message в RabbitMQ.
    /// </summary>
    private async Task<CollectionOutcom> FailMessagePublishingAsync(
        Sending sending,
        Message message,
        string errorMessage,
        Exception? possibleException,
        CancellationToken cancellationToken)
    {
        sending.SetStatus(SendingStatusEnum.ErrorOccuredWhilePublishingMessage);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        if (possibleException == null)
            await _errorNotifier.NotifyAboutErrorAsync(errorMessage, errorMessage);
        else
            await _errorNotifier.NotifyAboutErrorAsync(errorMessage, possibleException, errorMessage);

        await NotifyMessageFailed(sending, message, errorMessage, cancellationToken);

        return CollectionOutcom.Failed;
    }

    /// <summary>SSE-уведомление о провале сообщения.</summary>
    private async Task NotifyMessageFailed(Sending sending, Message message, string error, CancellationToken cancellationToken)
    {
        await _syncNotifier.NotifyAsync(sending.Id, new SyncEvent("MessageFailed", new
        {
            message.Id,
            Error = error,
            ErrorStatus = message.PublishingResultId.ToString()
        }), cancellationToken);
    }

    // ═══════════════════════════════════════════════════════════════════════════
    //  Публикация в RabbitMQ
    // ═══════════════════════════════════════════════════════════════════════════

    public async Task<(bool IsSuccess, Exception? PossibleException)> PublishMessage(
        Message message, CancellationToken cancellationToken)
    {
        var exchangeName = _rabbitMqQueues.CurrentValue.NsiTransferExchangeName;
        var queueName = _rabbitMqQueues.CurrentValue.PolynomSearchResultsQueueName;
        MessageFailure? failure = null;
        Exception? possibleException = null;

        try
        {
            var publishingResult = await _rabbitMqPublisher.PublishAsync(
                exchangeName, queueName,
                MediaTypeNames.Application.Json,
                message.Id.ToString(),
                message.SerializedMessage,
                cancellationToken);

            message.PublishingResultId = publishingResult;
            message.SentAtQueue = DateTime.UtcNow;

            if (publishingResult != RabbitMqPublishingResultEnum.Ack)
            {
                failure = new MessageFailure
                {
                    MessageId = message.Id,
                    FailedAt = DateTime.UtcNow,
                    FailureReasonTitle = "Результат публикации отличен от Ack",
                    FailureDescription = $"RabbitMQ вернул статус: {publishingResult}"
                };
            }
        }
        catch (OperationCanceledException cancelEx)
        {
            _logger.LogError(cancelEx, "Операция отправки в RabbitMQ была отменена для MessageId {MessageId}", message.Id);
            message.PublishingResultId = RabbitMqPublishingResultEnum.Failed;
            failure = CreateMessageFailure(message, cancelEx);
            possibleException = cancelEx;
        }
        catch (AggregateException aggEx)
        {
            _logger.LogError("Агрегированная ошибка при отправке в RabbitMQ для MessageId {MessageId}", message.Id);
            message.PublishingResultId = RabbitMqPublishingResultEnum.Failed;
            failure = CreateMessageFailure(message, aggEx);
            possibleException = aggEx;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Неожиданная ошибка при отправке в RabbitMQ для MessageId {MessageId}", message.Id);
            message.PublishingResultId = RabbitMqPublishingResultEnum.Failed;
            failure = CreateMessageFailure(message, ex);
            possibleException = ex;
        }

        message.MessageFailure = failure;
        return (failure == null, possibleException);
    }

    // ═══════════════════════════════════════════════════════════════════════════
    //  Вспомогательные методы
    // ═══════════════════════════════════════════════════════════════════════════

    private bool IsConceptNameForClassificationDataExists(PropertyOwnerResponseCustom propOwner)
    {
        var conceptName = _apiSyncOptions.CurrentValue.ConceptNameForClassificationData;
        return propOwner.AllContracts.Find(c => c.Name.Equals(conceptName, StringComparison.OrdinalIgnoreCase)) != null;
    }

    private async Task HandleErrorOccuredWhileCollection(
        Sending sending, Message message,
        string failureTitle, string failureDescription,
        CancellationToken cancellationToken)
    {
        var end = DateTime.UtcNow;
        message.PublishingResultId = RabbitMqPublishingResultEnum.FailedDuringInformationCollection;

        var failure = new MessageFailure
        {
            MessageId = message.Id,
            FailedAt = end,
            FailureReasonTitle = failureTitle,
            FailureDescription = failureDescription
        };

        message.MessageFailure = failure;
        await _unitOfWork.MessageFailures.AddAsync(failure, cancellationToken);

        sending.SetStatus(SendingStatusEnum.ErrorOccuredWhileCollectingDataForMessage, end);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
    }

    private static async Task HandleSyncError(
        Guid sendingId, Exception ex,
        IUnitOfWork unitOfWork, ILogger logger,
        ISyncNotifier notifier, bool backgroundTask, CancellationToken ct)
    {
        logger.LogError(ex, "Критическая ошибка{placeholder} синхронизации Полинома.", backgroundTask ? " фоновой" : "");

        try
        {
            var s = await unitOfWork.Sendings.FirstOrDefaultAsync(x => x.Id == sendingId, cancellationToken: ct);
            if (s != null)
            {
                s.SetStatus(SendingStatusEnum.ErrorUnknown);
                await unitOfWork.SaveChangesAsync(ct);
            }
        }
        catch (Exception exWriteDb)
        {
            var err = $"Критическая ошибка при записи статуса сущности Sending с SendingId '{sendingId}' " +
                      $"в БД после возникновения исключения в процессе выполнения задачи " +
                      $"{(backgroundTask ? "фоновой" : "")} синхронизации Полинома. НЕОБХОДИМО ПРОВЕРИТЬ СОЕДИНЕНИЕ С БД!";
            logger.LogError(exWriteDb, err);
            await notifier.NotifyAsync(sendingId,
                new SyncEvent("Error", new { Message = $"{err}\n {ex.Message}" }), ct);
        }

        await notifier.NotifyAsync(sendingId,
            new SyncEvent("Error", new { Message = $"Критическая ошибка {(backgroundTask ? "фоновой" : "")} синхронизации Полинома:\n {ex.Message}" }), ct);
    }

    private static MessageFailure CreateMessageFailure(Message message, Exception exception)
    {
        var mf = new MessageFailure
        {
            MessageId = message.Id,
            FailedAt = DateTime.UtcNow,
            FailureDescription = SerializeExceptionForStore(exception)
        };

        mf.FailureReasonTitle = exception is AggregateException aggEx
            ? aggEx.Flatten().InnerExceptions.FirstOrDefault()?.Message ?? aggEx.Message
            : exception.Message;

        return mf;
    }

    private static string SerializeExceptionForStore(Exception exception)
    {
        try
        {
            var payload = BuildExceptionPayload(exception);
            return JsonSerializer.Serialize(payload, ExceptionJsonOptions);
        }
        catch (Exception serializationEx)
        {
            var fallback = new
            {
                Error = "Failed to serialize exception",
                SerializationError = serializationEx.Message,
                OriginalException = exception.ToString()
            };
            return JsonSerializer.Serialize(fallback, ExceptionJsonOptions);
        }
    }

    private static object BuildExceptionPayload(Exception exception)
    {
        if (exception is AggregateException aggregate)
        {
            var flattened = aggregate.Flatten().InnerExceptions;
            var innerExceptions = new object[flattened.Count];
            for (var i = 0; i < flattened.Count; i++)
                innerExceptions[i] = BuildExceptionPayload(flattened[i]);

            return new
            {
                Type = exception.GetType().FullName,
                exception.Message,
                exception.StackTrace,
                exception.Source,
                exception.HResult,
                Data = ExtractExceptionData(exception),
                InnerExceptions = innerExceptions
            };
        }

        return new
        {
            Type = exception.GetType().FullName,
            exception.Message,
            exception.Source,
            exception.HResult,
            Data = ExtractExceptionData(exception),
            InnerException = exception.InnerException is null ? null : BuildExceptionPayload(exception.InnerException)
        };
    }

    private static IReadOnlyList<Dictionary<string, string?>>? ExtractExceptionData(Exception exception)
    {
        if (exception.Data is null || exception.Data.Count == 0)
            return null;

        var items = new List<Dictionary<string, string?>>(exception.Data.Count);
        foreach (var key in exception.Data.Keys)
        {
            var value = key is null ? null : exception.Data[key];
            items.Add(new Dictionary<string, string?>
            {
                ["Key"] = key?.ToString(),
                ["Value"] = value?.ToString()
            });
        }
        return items;
    }

    // ── Маппинг ──────────────────────────────────────────────────────────────

    private static SendingModel MapToModel(Sending sending) => new()
    {
        Id = sending.Id,
        InitiatedAt = sending.InitiatedAt,
        EndedAt = sending.EndedAt ?? default,
        InitiatorName = sending.InitiatorName,
        StatusId = sending.StatusId,
        Messages = sending.Messages?.Select(MapToModel).ToList() ?? []
    };

    private static MessageModel MapToModel(Message message) => new()
    {
        Id = message.Id,
        StartedCollectionFromPolynomAt = message.StartedCollectionFromPolynomAt,
        FinishedCollectionFromPolynomAt = message.FinishedCollectionFromPolynomAt,
        SentAtQueue = message.SentAtQueue,
        PolynomObjectsAmountInMessage = message.PolynomObjects?.Count ?? 0,
        PublishingResultId = message.PublishingResultId,
        MessageFailure = message.MessageFailure is null ? null : MapToModel(message.MessageFailure)
    };

    private static MessageFailureModel MapToModel(MessageFailure failure) => new()
    {
        MessageId = failure.MessageId,
        FailedAt = failure.FailedAt,
        FailureDescription = failure.FailureDescription,
        FailureReasonTitle = failure.FailureReasonTitle
    };
}

public enum CollectionOutcom { Completed, NoDataFound, Failed }