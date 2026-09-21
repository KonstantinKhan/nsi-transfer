using Ascon.Polynom.Web.Api.Data.Interfaces.Enums;
using Ascon.Polynom.Web.Api.Data.Models.Base;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NsiTransfer.BLL.Interfaces.Services;
using NsiTransfer.BLL.Tools;
using NsiTransfer.Contract;
using NsiTransfer.Contract.ConfigModels;
using NsiTransfer.Contract.Models.Common;
using NsiTransfer.Contract.Models.DTO;
using NsiTransfer.Contract.Models.Enums;
using NsiTransfer.DAL.Db.Entities;
using NsiTransfer.DAL.Interfaces.Db;

namespace NsiTransfer.BLL.Services;

internal sealed class ClassificationCodeProcessor : IClassificationCodeProcessor
{
    private readonly IPolynomApiService _polynomApiService;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IOptionsMonitor<PolynomApiSyncOptions> _apiSyncOptions;
    private readonly IOptionsMonitor<Contract.ConfigModels.TargetReferenceNode> _targetRefNode;
    private readonly ILogger<ClassificationCodeProcessor> _logger;

    // Кеш последнего выданного кода по группе в рамках одного sync run (экземпляр — Scoped, живёт один запуск).
    // Обход дочерних объектов группы через API Полинома дорогой — не повторяем его для каждого объекта группы.
    private readonly Dictionary<(int ObjectId, IdentifiableObjectType TypeId), string> _groupLastCodeCache = new();

    // Очередь свободных номеров для групп, у которых диапазон кодов оказался исчерпан в рамках текущего run
    // (объекты удалялись из середины группы — образовались свободные номера). Заполняется один раз при первом
    // исчерпании группы (полный обход через GetAllClassificationCodesInGroup), дальше только Dequeue — O(1).
    // Присутствие ключа в этом словаре означает "группа в режиме поиска свободных номеров" — обычный increment
    // для неё больше не используется (иначе пересечёмся с уже занятыми кодами выше по диапазону).
    // Truncated == true значит: сканирование упёрлось в MaxFreeCodeScanIterations и очередь неполная —
    // в диапазоне могут оставаться свободные номера, которые не попали в очередь (см. ComputeFreeCodesQueue).
    private readonly Dictionary<(int ObjectId, IdentifiableObjectType TypeId), (Queue<string> Queue, bool Truncated)> _groupFreeCodesCache = new();

    private const int MaxFreeCodeScanIterations = 500_000;

    public ClassificationCodeProcessor(
        IPolynomApiService polynomApiService,
        IUnitOfWork unitOfWork,
        IOptionsMonitor<PolynomApiSyncOptions> apiSyncOptions,
        IOptionsMonitor<Contract.ConfigModels.TargetReferenceNode> targetRefNode,
        ILogger<ClassificationCodeProcessor> logger)
    {
        _polynomApiService = polynomApiService;
        _unitOfWork = unitOfWork;
        _apiSyncOptions = apiSyncOptions;
        _targetRefNode = targetRefNode;
        _logger = logger;
    }

    /// <summary>
    /// Главный вход в flow. Возвращает PolynomObject-лог при успехе или ошибку при неудаче.
    /// </summary>
    public async Task<Result<DAL.Db.Entities.PolynomObject>> ProcessAsync(
        PolynomObjectWithShortProperties mappedInOutputModel,
        Message message,
        CancellationToken cancellationToken)
    {
        // Получаем свойство "Код классификатора" у самого объекта
        var contract = GetContractForClassificationData(mappedInOutputModel);
        var prop = GetShortPropertyInContract(contract);

        if (contract == null || prop == null)
        {
            return await FailObjectAsync(
                message, mappedInOutputModel,
                PolynomObjectFailureTypeEnum.CannotFindPropertyClassificationCode,
                $"Не удалось найти свойство '{_apiSyncOptions.CurrentValue.ClassificationCodePropertyName}' " +
                $"в понятии '{_apiSyncOptions.CurrentValue.ConceptNameForClassificationData}' {ForObjectConstructString(mappedInOutputModel)}",
                cancellationToken);
        }


        // Если у объекта уже есть значение кода — проверяем, что он находится в допустимом диапазоне группы,
        // потом формируем лог-запись с уже существующим значением
        if (!string.IsNullOrWhiteSpace(prop.Value))
        {
            var existingCode = prop.Value;
            var parentGroupsResult = await _polynomApiService.GetParentGroupsWithProperties(mappedInOutputModel.ObjectId, mappedInOutputModel.TypeId, cancellationToken);

            if (parentGroupsResult.IsSuccess && parentGroupsResult.Data!.Count > 0)
            {
                var parentGroups = parentGroupsResult.Data!;

                // Выбираем группу с Min/Max свойствами
                PolynomObjectWithShortProperties? correctGroup = parentGroups.Count switch
                {
                    1 => parentGroups[0],
                    > 1 => parentGroups.FirstOrDefault(g => HasMinMaxProps(g)),
                    _ => null
                };

                if (correctGroup != null)
                {
                    var leafCheck = await EnsureGroupIsLeafAsync(correctGroup, mappedInOutputModel, message, cancellationToken);
                    if (leafCheck != null) return leafCheck;

                    mappedInOutputModel.GroupInfo = BuildGroupInfo(correctGroup, isLeaf: true);

                    var (minProp, maxProp) = GetMinMaxProps(correctGroup);
                    if (minProp != null && maxProp != null && minProp.Value != null && maxProp.Value != null)
                    {
                        var comparer = NumericStringComparer.Instance;

                        if (comparer.Compare(existingCode, minProp.Value) < 0 || comparer.Compare(existingCode, maxProp.Value) > 0)
                        {
                            return await FailObjectAsync(
                                message, mappedInOutputModel,
                                PolynomObjectFailureTypeEnum.ClassificationCodeOutOfGroupRange,
                                $"Код классификатора '{existingCode}' находится вне допустимого диапазона [{minProp.Value}, {maxProp.Value}] группы '{correctGroup.Name}' {ForObjectConstructString(mappedInOutputModel)}",
                                cancellationToken);
                        }
                    }
                }
            }

            return CreatePolynomObjectLog(mappedInOutputModel, message, existingCode);
        }

        // Кода нет - нужно вычислить через родительские группы.
        var parentGroupsForCalculationResult = await _polynomApiService.GetParentGroupsWithProperties(mappedInOutputModel.ObjectId, mappedInOutputModel.TypeId, cancellationToken);

        if (!parentGroupsForCalculationResult.IsSuccess)
        {
            return await FailObjectAsync(
                message, mappedInOutputModel,
                PolynomObjectFailureTypeEnum.ErrorWhileProcessWasExecuting,
                parentGroupsForCalculationResult.ErrorMessage!,
                cancellationToken);
        }

        var parentGroupsForNewCode = parentGroupsForCalculationResult.Data!;

        // Определяем "корректную" родительскую группу и возможные ошибки
        PolynomObjectWithShortProperties? correctGroupForNewCode = null;
        PolynomObjectFailureTypeEnum? failureType = null;
        string? failureMessage = null;

        Console.WriteLine($"Количество найденных групп: {parentGroupsForNewCode.Count}");

        foreach(PolynomObjectWithShortProperties group in parentGroupsForNewCode)
        {
            Console.WriteLine($"group: {group.Name}");
        }

        switch (parentGroupsForNewCode.Count)
        {
            case 0:
                failureType = PolynomObjectFailureTypeEnum.NoGroupsAtAll;
                failureMessage = $"Не найдена ни одна родительская группа {ForObjectConstructString(mappedInOutputModel)}";
                break;

            case > 1:
                try
                {
                    correctGroupForNewCode = parentGroupsForNewCode.SingleOrDefault(group => HasMinMaxProps(group));
                }
                catch (InvalidOperationException)
                {
                    failureType = PolynomObjectFailureTypeEnum.MultipleGroupsWithMinMax;
                    failureMessage = $"Несколько родительских групп с минимальными и максимальными значениями кодов " +
                                     $"{ForObjectConstructString(mappedInOutputModel)}. Невозможно выбрать конкретную.";
                }

                // ни одна группа не содержит необходимых свойств Min/Max
                if (correctGroupForNewCode == null && failureType == null)
                {
                    failureType = PolynomObjectFailureTypeEnum.NoGroupsWithMinMax;
                    failureMessage = $"Нет родительской группы с минимальными и максимальными значениями кодов {ForObjectConstructString(mappedInOutputModel)}";
                }
                break;

            default: // ровно 1 группа
                correctGroupForNewCode = parentGroupsForNewCode[0];
                break;
        }

        if (failureType.HasValue)
        {
            return await FailObjectAsync(message, mappedInOutputModel, failureType.Value, failureMessage!, cancellationToken);
        }

        var leafCheckForNewCode = await EnsureGroupIsLeafAsync(correctGroupForNewCode!, mappedInOutputModel, message, cancellationToken);
        if (leafCheckForNewCode != null) return leafCheckForNewCode;

        mappedInOutputModel.GroupInfo = BuildGroupInfo(correctGroupForNewCode!, isLeaf: true);

        // Обрабатываем единственную/выбранную группу.
        try
        {
            return await HandleOneGroupAsync(correctGroupForNewCode!, mappedInOutputModel, message, cancellationToken);
        }
        catch (InvalidOperationException ex)
        {
            // Код превышает максимум в группе — обрабатываем как нормальную ошибку, не даём пробраться до уровня batch-abort.
            return await FailObjectAsync(
                message, mappedInOutputModel,
                PolynomObjectFailureTypeEnum.ErrorWhileProcessWasExecuting,
                ex.Message,
                cancellationToken);
        }
    }

    /// <summary>
    /// Обработка одной группы: проверка наличия Min/Max, получение последнего кода, создание нового.
    /// </summary>
    private async Task<Result<DAL.Db.Entities.PolynomObject>> HandleOneGroupAsync(
        PolynomObjectWithShortProperties group,
        PolynomObjectWithShortProperties mappedInOutputModel,
        Message message,
        CancellationToken cancellationToken)
    {
        var (minProp, maxProp) = GetMinMaxProps(group);

        if (minProp == null || maxProp == null || minProp.Value == null || maxProp.Value == null)
        {
            var msg = $"Не найдены свойства '{_apiSyncOptions.CurrentValue.MinCodePropertyName}' и '{_apiSyncOptions.CurrentValue.MaxCodePropertyName}', " +
                      $"или свойств с такими именами несколько у родительской группы, " +
                      $"или свойства не имеют значений " +
                      $"{ForObjectConstructString(mappedInOutputModel)}";
            return await FailObjectAsync(
                message, mappedInOutputModel,
                PolynomObjectFailureTypeEnum.GroupWithoutMinMax, msg, cancellationToken);
        }

        var groupCacheKey = (group.ObjectId, group.TypeId);
        var comparer = NumericStringComparer.Instance;
        string newCode;

        if (_groupFreeCodesCache.TryGetValue(groupCacheKey, out var activeFreeQueue))
        {
            // Группа уже помечена как исчерпанная в этом run — продолжаем выдавать свободные номера из очереди.
            // НЕ используем обычный increment здесь: последний закешированный "максимум" уже равен верхней границе
            // диапазона, инкремент от него снова превысил бы её.
            if (activeFreeQueue.Queue.Count == 0)
            {
                throw new InvalidOperationException(BuildFreeCodesExhaustedMessage(activeFreeQueue.Truncated, mappedInOutputModel));
            }

            newCode = activeFreeQueue.Queue.Dequeue();
        }
        else
        {
            // Последний выданный код ищем сначала в кеше группы (в рамках текущего sync run) —
            // это позволяет не обходить дочерние объекты группы через API повторно для каждого объекта.
            string? lastCode;

            if (_groupLastCodeCache.TryGetValue(groupCacheKey, out var cachedLastCode))
            {
                lastCode = cachedLastCode;
            }
            else
            {
                // Персистентный кеш (между sync run'ами) — если для группы уже есть строка, используем её значение
                // как отправную точку вместо полного обхода дочерних объектов группы через Polynom API.
                // ВАЖНО: если объекты в этой группе создавались вручную в Polynom (в обход NsiTransfer), кеш может
                // быть устаревшим — используйте кнопку переиндексации (RebuildGroupCodeCacheAsync).
                var persistedRow = await _unitOfWork.ClassificationGroupCodeMaxes.FirstOrDefaultAsync(
                    predicate: r => r.GroupObjectId == group.ObjectId && r.GroupTypeId == group.TypeId,
                    cancellationToken: cancellationToken);

                if (persistedRow != null)
                {
                    lastCode = persistedRow.LastMaxCode;
                }
                else
                {
                    // Персистентного кеша ещё нет для этой группы — получаем максимальный существующий код
                    // среди дочерних объектов группы (дорогая операция, полный обход группы).
                    var lastCodeResult = await _polynomApiService.GetLastClassificationCodeInGroup(group.ObjectId, group.TypeId, minProp.Value!, maxProp.Value!, cancellationToken)
                        .OnFailureAsync((err) => err.AddError(
                                $"Ошибка получения максимального значения свойства " +
                                $"'{_apiSyncOptions.CurrentValue.ClassificationCodePropertyName}' среди всех " +
                                $"дочерних объектов родительской группы {ForObjectConstructString(mappedInOutputModel)}"));

                    if (!lastCodeResult.IsSuccess)
                    {
                        return await FailObjectAsync(
                            message, mappedInOutputModel,
                            PolynomObjectFailureTypeEnum.ErrorWhileProcessWasExecuting,
                            lastCodeResult.ErrorMessage!, cancellationToken);
                    }

                    lastCode = lastCodeResult.Data;
                }
            }

            // Если данных нет (никто ещё не задавал код), стартуем от MinValue
            var candidateCode = lastCode?.Increment() ?? minProp.Value;

            if (comparer.Compare(candidateCode, minProp.Value) < 0)
                throw new InvalidOperationException($"Новое значение кода классификации меньше минимально допустимого в группе {ForObjectConstructString(mappedInOutputModel)}");

            if (comparer.Compare(candidateCode, maxProp.Value) > 0)
            {
                // Диапазон исчерпан по вычисленному максимуму — ищем свободные номера внутри диапазона
                // (объекты могли быть удалены из середины группы, освободив номера). Полный обход группы —
                // редкая операция, срабатывает только здесь, не на обычном пути.
                var freeQueueResult = await BuildFreeCodesQueueAsync(group, minProp.Value!, maxProp.Value!, cancellationToken);

                if (!freeQueueResult.IsSuccess)
                {
                    throw new InvalidOperationException(
                        $"Новое значение кода классификации превышает максимально допустимый в группе, поиск свободных номеров завершился ошибкой: " +
                        $"{freeQueueResult.ErrorMessage} {ForObjectConstructString(mappedInOutputModel)}");
                }

                var freeQueue = freeQueueResult.Data!;
                _groupFreeCodesCache[groupCacheKey] = freeQueue;

                if (freeQueue.Truncated)
                {
                    _logger.LogWarning(
                        "Поиск свободных номеров кода классификации для группы (objectId {ObjectId}, typeId {TypeId}) прерван по лимиту итераций ({Limit}) — диапазон группы слишком большой/разреженный, часть свободных номеров могла быть не учтена.",
                        group.ObjectId, group.TypeId, MaxFreeCodeScanIterations);
                }

                if (freeQueue.Queue.Count == 0)
                {
                    throw new InvalidOperationException(BuildFreeCodesExhaustedMessage(freeQueue.Truncated, mappedInOutputModel));
                }

                newCode = freeQueue.Queue.Dequeue();
                // ВАЖНО: не обновляем _groupLastCodeCache/персистентный кеш здесь — группа исчерпана по верхней
                // границе, значение "последнего максимума" остаётся прежним (равным maxValue), дальнейшие объекты
                // этой группы (в этом и в следующих run'ах) должны продолжать поиск свободных номеров, не increment.
            }
            else
            {
                newCode = candidateCode;

                // Резервируем код за группой сразу — следующий объект той же группы продолжит с этого значения.
                _groupLastCodeCache[groupCacheKey] = newCode;

                // Пишем и в персистентный кеш — следующий sync run начнёт отсюда, без обхода группы через API.
                // Фактический SaveChangesAsync вызовет вызывающий код (SyncUseCases) вместе с остальными изменениями батча.
                await UpsertGroupCodeMaxAsync(group.ObjectId, group.TypeId, newCode, cancellationToken);
            }
        }

        // Создаём новый код и отправляем его в Полином
        return await SetNewClassificationCodeAsync(mappedInOutputModel, message, newCode, cancellationToken);
    }

    /// <summary>
    /// Устанавливает новое значение кода классификатора:
    /// - на замапленную модель (для последующей отправки в RabbitMQ);
    /// - отправляет запрос в Полином на обновление свойства;
    /// - формирует PolynomObject-лог для БД.
    /// </summary>
    private async Task<Result<DAL.Db.Entities.PolynomObject>> SetNewClassificationCodeAsync(
        PolynomObjectWithShortProperties mappedInOutputModel,
        Message message,
        string newClassificationCode,
        CancellationToken cancellationToken)
    {
        var contract = GetContractForClassificationData(mappedInOutputModel);
        var prop = GetShortPropertyInContract(contract);

        if (contract == null || prop == null)
        {
            return await FailObjectAsync(
                message, mappedInOutputModel,
                PolynomObjectFailureTypeEnum.CannotFindPropertyClassificationCode,
                $"Не удалось найти свойство '{_apiSyncOptions.CurrentValue.ClassificationCodePropertyName}' " +
                $"в понятии '{_apiSyncOptions.CurrentValue.ConceptNameForClassificationData}' {ForObjectConstructString(mappedInOutputModel)}",
                cancellationToken);
        }

        // Устанавливаем на замапленную модель — она пойдёт в RabbitMQ.
        prop.Value = newClassificationCode;

        // Отправляем запрос в Полином на обновление свойства.
        var updateResult = await _polynomApiService.UpdateClassificationCodeAsync(mappedInOutputModel, contract, prop, cancellationToken);

        if (!updateResult.IsSuccess)
        {
            return await FailObjectAsync(
                message, mappedInOutputModel,
                PolynomObjectFailureTypeEnum.ErrorWhileProcessWasExecuting,
                updateResult.ErrorMessage!,
                cancellationToken);
        }

        // Формируем лог-запись для БД.
        return CreatePolynomObjectLog(mappedInOutputModel, message, newClassificationCode);
    }

    // ─────────────────────────────────────────────────────────────────────────────
    //  Вспомогательные методы
    // ─────────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Единая точка обработки ошибки классификатора:
    /// сохраняет PolynomObjectFailure в БД и возвращает Result с ошибкой.
    /// Email-уведомления отправляются вызывающим кодом (SyncUseCases) на уровне сообщения.
    /// </summary>
    private async Task<Result<DAL.Db.Entities.PolynomObject>> FailObjectAsync(
        Message message,
        PolynomObjectWithShortProperties model,
        PolynomObjectFailureTypeEnum failureType,
        string errorMessage,
        CancellationToken cancellationToken)
    {
        _logger.LogWarning("ClassificationCode error: {FailureType} | {ErrorMessage}", failureType, errorMessage);

        var existingFailures = await _unitOfWork.PolynomObjectFailures.GetAllAsync(
            predicate: f => f.PolynomObjectId == model.ObjectId && f.PolynomTypeId == model.TypeId,
            cancellationToken: cancellationToken);

        if (existingFailures.Count > 0)
        {
            _unitOfWork.PolynomObjectFailures.DeleteRange(existingFailures);
        }

        var failure = new PolynomObjectFailure
        {
            Message = message,
            FailureType = failureType,
            FailedAt = DateTime.UtcNow,
            PolynomObjectId = model.ObjectId,
            PolynomTypeId = model.TypeId,
            ObjectName = model.Name,
            ErrorMessage = errorMessage
        };

        await _unitOfWork.PolynomObjectFailures.AddAsync(failure, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return errorMessage;
    }

    private async Task<Result<DAL.Db.Entities.PolynomObject>?> EnsureGroupIsLeafAsync(
        PolynomObjectWithShortProperties group,
        PolynomObjectWithShortProperties model,
        Message message,
        CancellationToken cancellationToken)
    {
        var leafResult = await _polynomApiService.IsGroupLeafAsync(group.ObjectId, group.TypeId, cancellationToken);

        if (!leafResult.IsSuccess)
        {
            return await FailObjectAsync(
                message, model,
                PolynomObjectFailureTypeEnum.ErrorWhileProcessWasExecuting,
                leafResult.ErrorMessage!, cancellationToken);
        }

        if (!leafResult.Data)
        {
            return await FailObjectAsync(
                message, model,
                PolynomObjectFailureTypeEnum.GroupIsNotLeaf,
                $"Группа '{group.Name}' содержит вложенные подгруппы, объект должен находиться в конечной группе классификатора {ForObjectConstructString(model)}",
                cancellationToken);
        }

        return null;
    }

    private static DAL.Db.Entities.PolynomObject CreatePolynomObjectLog(PolynomObjectWithShortProperties incomeModel, Message message, string classificationCode)
        => new()
        {
            Name = incomeModel.Name,
            ClassificationCode = classificationCode,
            PolynomObjectId = incomeModel.ObjectId,
            PolynomTypeId = incomeModel.TypeId,
            Message = message
        };

    private PolynomContractWithShortProperties? GetContractForClassificationData(PolynomObjectWithShortProperties model)
    {
        var conceptName = _apiSyncOptions.CurrentValue.ConceptNameForClassificationData;
        var contract = model.Contracts.Find(c => c.Name.Equals(conceptName, StringComparison.OrdinalIgnoreCase));
        return contract;
    }

    private PolynomShortProperty? GetShortPropertyInContract(PolynomContractWithShortProperties? contract)
    {
        var propName = _apiSyncOptions.CurrentValue.ClassificationCodePropertyName;
        var prop = contract?.Properties.Find(p => p.Name.Equals(propName, StringComparison.OrdinalIgnoreCase));
        return prop;
    }

    private bool HasMinMaxProps(PolynomObjectWithShortProperties model)
    {
        var (minProp, maxProp) = GetMinMaxProps(model);
        return minProp != null && maxProp != null;
    }

    private (PolynomShortProperty? MinProp, PolynomShortProperty? MaxProp) GetMinMaxProps(PolynomObjectWithShortProperties parent)
    {
        var ownContract = parent.Contracts
            .Find(c => c.Name.Equals(_apiSyncOptions.CurrentValue.OwnContractName, StringComparison.OrdinalIgnoreCase));

        if (ownContract == null) return (null, null);

        try
        {
            var minProp = ownContract.Properties.SingleOrDefault(p =>
                p.Name.Equals(_apiSyncOptions.CurrentValue.MinCodePropertyName, StringComparison.OrdinalIgnoreCase));
            var maxProp = ownContract.Properties.SingleOrDefault(p =>
                p.Name.Equals(_apiSyncOptions.CurrentValue.MaxCodePropertyName, StringComparison.OrdinalIgnoreCase));
            return (minProp, maxProp);
        }
        catch
        {
            // Если несколько свойств с тем или иным именем, то считаем ошибкой
            return (null, null);
        }
    }

    /// <summary>
    /// Собирает GroupInfo для JSON объекта из уже загруженных данных группы — без дополнительных запросов
    /// к Polynom API (min/max свойства уже получены через GetParentGroupsWithProperties, isLeaf уже проверен
    /// через EnsureGroupIsLeafAsync к моменту вызова).
    /// </summary>
    private GroupInfo BuildGroupInfo(PolynomObjectWithShortProperties group, bool isLeaf)
    {
        var (minProp, maxProp) = GetMinMaxProps(group);

        return new GroupInfo
        {
            ObjectId = group.ObjectId,
            TypeId = group.TypeId,
            Name = group.Name,
            MinCode = minProp?.Value,
            MaxCode = maxProp?.Value,
            IsLeaf = isLeaf
        };
    }

    private string ForObjectConstructString(PolynomObjectWithShortProperties model)
        => $"у объекта с objectId '{model.ObjectId}' и typeId '{model.TypeId}' " +
           $"с наименованием '{model.Name}'";

    /// <summary>
    /// Записывает/обновляет строку персистентного кеша последнего кода группы.
    /// Не вызывает SaveChangesAsync — изменение уедет вместе с остальными изменениями текущего batch.
    /// </summary>
    private async Task UpsertGroupCodeMaxAsync(int groupObjectId, IdentifiableObjectType groupTypeId, string? newLastMaxCode, CancellationToken cancellationToken)
    {
        var existingRow = await _unitOfWork.ClassificationGroupCodeMaxes.FirstOrDefaultAsync(
            predicate: r => r.GroupObjectId == groupObjectId && r.GroupTypeId == groupTypeId,
            cancellationToken: cancellationToken);

        if (existingRow != null)
        {
            existingRow.LastMaxCode = newLastMaxCode;
            existingRow.UpdatedAt = DateTime.UtcNow;
            _unitOfWork.ClassificationGroupCodeMaxes.Update(existingRow);
        }
        else
        {
            await _unitOfWork.ClassificationGroupCodeMaxes.AddAsync(new ClassificationGroupCodeMax
            {
                GroupObjectId = groupObjectId,
                GroupTypeId = groupTypeId,
                LastMaxCode = newLastMaxCode,
                UpdatedAt = DateTime.UtcNow
            }, cancellationToken);
        }
    }

    // ─────────────────────────────────────────────────────────────────────────────
    //  Поиск свободных номеров кода в группе (исчерпан диапазон — объекты удалялись из середины)
    // ─────────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Полный обход группы через Polynom API, вычисление ВСЕХ свободных номеров в диапазоне [minValue, maxValue]
    /// и упаковка их в очередь (по возрастанию). Дорогая операция — вызывается только когда обычный increment
    /// превысил верхнюю границу диапазона группы.
    /// </summary>
    private async Task<Result<(Queue<string> Queue, bool Truncated)>> BuildFreeCodesQueueAsync(
        PolynomObjectWithShortProperties group, string minValue, string maxValue, CancellationToken cancellationToken)
    {
        var usedCodesResult = await _polynomApiService.GetAllClassificationCodesInGroup(group.ObjectId, group.TypeId, minValue, maxValue, cancellationToken);

        if (!usedCodesResult.IsSuccess)
        {
            return Result<(Queue<string>, bool)>.Failure(usedCodesResult.ErrorMessage);
        }

        return Result<(Queue<string>, bool)>.Success(ComputeFreeCodesQueue(usedCodesResult.Data!, minValue, maxValue));
    }

    private string BuildFreeCodesExhaustedMessage(bool truncated, PolynomObjectWithShortProperties model)
        => truncated
            ? $"Новое значение кода классификации превышает максимально допустимый в группе. Поиск свободных номеров " +
              $"прерван по лимиту итераций ({MaxFreeCodeScanIterations}) — диапазон группы слишком большой/разреженный " +
              $"для полного сканирования за один проход, часть свободных номеров могла остаться неучтённой. " +
              $"{ForObjectConstructString(model)}"
            : $"Новое значение кода классификации превышает максимально допустимый в группе, свободных номеров не осталось " +
              $"{ForObjectConstructString(model)}";

    /// <summary>
    /// Вычисляет все свободные (не занятые) коды в диапазоне [minValue, maxValue], которых нет в usedCodes.
    /// Идёт по отсортированным занятым кодам и собирает пропуски между ними — стоимость пропорциональна
    /// количеству реально занятых объектов группы, а не размеру диапазона кодов (за исключением случая, когда
    /// сам диапазон кодов огромен и разрежен — см. MaxFreeCodeScanIterations и Truncated).
    /// Truncated == true означает: упёрлись в лимит итераций, очередь неполная (могут быть ещё свободные номера
    /// дальше по диапазону, которые не попали в очередь).
    /// </summary>
    private static (Queue<string> Queue, bool Truncated) ComputeFreeCodesQueue(HashSet<string> usedCodes, string minValue, string maxValue)
    {
        var comparer = NumericStringComparer.Instance;

        var sortedUsed = usedCodes
            .Where(c => comparer.Compare(c, minValue) >= 0 && comparer.Compare(c, maxValue) <= 0)
            .Distinct()
            .OrderBy(c => c, comparer)
            .ToList();

        var freeQueue = new Queue<string>();
        var candidate = minValue;
        var iterations = 0;

        foreach (var used in sortedUsed)
        {
            while (comparer.Compare(candidate, used) < 0)
            {
                freeQueue.Enqueue(candidate);
                candidate = candidate.Increment();
                if (++iterations > MaxFreeCodeScanIterations) return (freeQueue, true);
            }

            if (comparer.Compare(candidate, used) == 0)
            {
                candidate = candidate.Increment();
                if (++iterations > MaxFreeCodeScanIterations) return (freeQueue, true);
            }
        }

        while (comparer.Compare(candidate, maxValue) <= 0)
        {
            freeQueue.Enqueue(candidate);
            candidate = candidate.Increment();
            if (++iterations > MaxFreeCodeScanIterations) return (freeQueue, true);
        }

        return (freeQueue, false);
    }

    // ─────────────────────────────────────────────────────────────────────────────
    //  Переиндексация персистентного кеша (ручной запуск, кнопка "индексация")
    // ─────────────────────────────────────────────────────────────────────────────

    private const int RebuildMaxDepth = 20;
    private const int RebuildMaxErrors = 100;

    public async Task<Result<GroupCodeCacheRebuildResult>> RebuildGroupCodeCacheAsync(CancellationToken cancellationToken)
    {
        var summary = new GroupCodeCacheRebuildResult();

        var targetObjectId = _targetRefNode.CurrentValue.TargetReferenceNodeObjectId;
        var targetTypeId = (IdentifiableObjectType)_targetRefNode.CurrentValue.TargetReferenceNodeTypeId;
        var targetName = _targetRefNode.CurrentValue.TargetReferenceNodeName;

        _logger.LogInformation("Переиндексация кеша кодов классификатора: старт со справочника '{Name}' (objectId {ObjectId}, typeId {TypeId})",
            targetName, targetObjectId, targetTypeId);

        // Иерархия: Справочник (Reference) → Каталог (Catalog) → Группа (Group, дальше рекурсивно через
        // WalkAndIndexGroupAsync/GetSubGroups). Справочник сам по себе не является ни группой, ни каталогом —
        // element-group/get-by-group на его objectId возвращает 404, поэтому идём по правильной цепочке уровней.
        var catalogsResult = await _polynomApiService.GetCatalogsByReference(targetObjectId, targetTypeId, cancellationToken);
        if (!catalogsResult.IsSuccess)
        {
            return $"Не удалось получить каталоги справочника '{targetName}' (objectId '{targetObjectId}', typeId '{targetTypeId}') для переиндексации: {catalogsResult.ErrorMessage}";
        }

        if (catalogsResult.Data!.Count == 0)
        {
            return $"Справочник '{targetName}' (objectId '{targetObjectId}', typeId '{targetTypeId}') не содержит каталогов — переиндексация невозможна.";
        }

        foreach (var catalog in catalogsResult.Data!)
        {
            var groupsResult = await _polynomApiService.GetGroupsByCatalog(catalog.ObjectId, catalog.TypeId, cancellationToken);
            if (!groupsResult.IsSuccess)
            {
                summary.GroupsWithErrors++;
                AddRebuildError(summary, $"Ошибка получения групп каталога '{catalog.Name}' (objectId {catalog.ObjectId}): {groupsResult.ErrorMessage}");
                continue;
            }

            foreach (var group in groupsResult.Data!)
            {
                await WalkAndIndexGroupAsync(group.ObjectId, group.TypeId, group.Name, 0, summary, cancellationToken);
            }
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation(
            "Переиндексация кеша кодов классификатора завершена. Проиндексировано групп: {Indexed}, пропущено (не группа классификатора): {Skipped}, с ошибками: {Errors}",
            summary.GroupsIndexed, summary.GroupsSkippedNotAClassificationGroup, summary.GroupsWithErrors);

        return summary;
    }

    /// <summary>
    /// Рекурсивный обход ПОДГРУПП (element-group/get-by-group — та же семантика, что IsGroupLeafAsync
    /// в обычном flow). Принципиально НЕ используем классификационное дерево (GetClassificationChildrenNodes) —
    /// оно возвращает и элементы/объекты группы тоже, обход проваливался бы в конкретные товары вместо групп.
    /// </summary>
    private async Task WalkAndIndexGroupAsync(
        int objectId, IdentifiableObjectType typeId, string name, int depth, GroupCodeCacheRebuildResult summary, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (depth > RebuildMaxDepth)
        {
            AddRebuildError(summary, $"Достигнута максимальная глубина обхода ({RebuildMaxDepth}) на группе '{name}' (objectId {objectId}) — дальнейший обход этой ветки пропущен.");
            return;
        }

        var subGroupsResult = await _polynomApiService.GetSubGroups(objectId, typeId, cancellationToken);
        if (!subGroupsResult.IsSuccess)
        {
            summary.GroupsWithErrors++;
            AddRebuildError(summary, $"Ошибка получения подгрупп для '{name}' (objectId {objectId}): {subGroupsResult.ErrorMessage}");
            return;
        }

        var subGroups = subGroupsResult.Data!;

        if (subGroups.Count == 0)
        {
            // Нет вложенных подгрупп — конечная группа классификатора. Пытаемся проиндексировать, если есть Min/Max.
            await IndexOneGroupAsync(objectId, typeId, name, summary, cancellationToken);
            return;
        }

        foreach (var sub in subGroups)
        {
            await WalkAndIndexGroupAsync(sub.ObjectId, sub.TypeId, sub.Name, depth + 1, summary, cancellationToken);
        }
    }

    private async Task IndexOneGroupAsync(int objectId, IdentifiableObjectType typeId, string name, GroupCodeCacheRebuildResult summary, CancellationToken cancellationToken)
    {
        var propsResult = await _polynomApiService.GetAllPropertiesOfObject(objectId, typeId, cancellationToken);
        if (!propsResult.IsSuccess)
        {
            summary.GroupsWithErrors++;
            AddRebuildError(summary, $"Ошибка получения свойств группы '{name}' (objectId {objectId}): {propsResult.ErrorMessage}");
            return;
        }

        var namedObject = new NamedObject { ObjectId = objectId, TypeId = typeId, Name = name };
        var mappedGroup = ModelMapper.CreateObjectWithShortProperties(namedObject, propsResult.Data!);
        var (minProp, maxProp) = GetMinMaxProps(mappedGroup);

        if (minProp?.Value == null || maxProp?.Value == null)
        {
            // Обычная папка-категория без Min/Max свойств — не группа классификатора, пропускаем молча.
            summary.GroupsSkippedNotAClassificationGroup++;
            return;
        }

        var lastCodeResult = await _polynomApiService.GetLastClassificationCodeInGroup(objectId, typeId, minProp.Value, maxProp.Value, cancellationToken);
        if (!lastCodeResult.IsSuccess)
        {
            summary.GroupsWithErrors++;
            AddRebuildError(summary, $"Ошибка вычисления последнего кода для группы '{name}' (objectId {objectId}): {lastCodeResult.ErrorMessage}");
            return;
        }

        await UpsertGroupCodeMaxAsync(objectId, typeId, lastCodeResult.Data, cancellationToken);
        _groupLastCodeCache[(objectId, typeId)] = lastCodeResult.Data ?? minProp.Value;
        summary.GroupsIndexed++;

        // Периодически сохраняем — не держим тысячи отслеживаемых изменений в одной транзакции.
        if (summary.GroupsIndexed % 50 == 0)
        {
            await _unitOfWork.SaveChangesAsync(cancellationToken);
            _logger.LogInformation("Переиндексация кеша кодов классификатора: проиндексировано {Count} групп...", summary.GroupsIndexed);
        }
    }

    private static void AddRebuildError(GroupCodeCacheRebuildResult summary, string error)
    {
        if (summary.Errors.Count < RebuildMaxErrors)
        {
            summary.Errors.Add(error);
        }
    }
}
