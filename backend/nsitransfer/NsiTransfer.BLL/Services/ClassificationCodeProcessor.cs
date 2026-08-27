using Ascon.Polynom.Web.Api.Data.Interfaces.Enums;
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
    private readonly ILogger<ClassificationCodeProcessor> _logger;

    // Кеш последнего выданного кода по группе в рамках одного sync run (экземпляр — Scoped, живёт один запуск).
    // Обход дочерних объектов группы через API Полинома дорогой — не повторяем его для каждого объекта группы.
    private readonly Dictionary<(int ObjectId, IdentifiableObjectType TypeId), string> _groupLastCodeCache = new();

    public ClassificationCodeProcessor(
        IPolynomApiService polynomApiService,
        IUnitOfWork unitOfWork,
        IOptionsMonitor<PolynomApiSyncOptions> apiSyncOptions,
        ILogger<ClassificationCodeProcessor> logger)
    {
        _polynomApiService = polynomApiService;
        _unitOfWork = unitOfWork;
        _apiSyncOptions = apiSyncOptions;
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

        // Последний выданный код ищем сначала в кеше группы (в рамках текущего sync run) —
        // это позволяет не обходить дочерние объекты группы через API повторно для каждого объекта.
        var groupCacheKey = (group.ObjectId, group.TypeId);
        string? lastCode;

        if (_groupLastCodeCache.TryGetValue(groupCacheKey, out var cachedLastCode))
        {
            lastCode = cachedLastCode;
        }
        else
        {
            // Получаем максимальный существующий код среди дочерних объектов группы.
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

        // Если данных нет (никто ещё не задавал код), стартуем от MinValue
        var newCode = lastCode?.Increment() ?? minProp.Value;
        var comparer = NumericStringComparer.Instance;

        if (comparer.Compare(newCode, minProp.Value) < 0)
            throw new InvalidOperationException($"Новое значение кода классификации меньше минимально допустимого в группе {ForObjectConstructString(mappedInOutputModel)}");

        if (comparer.Compare(newCode, maxProp.Value) > 0)
            throw new InvalidOperationException($"Новое значение кода классификации превышает максимально допустимый в группе {ForObjectConstructString(mappedInOutputModel)}");

        // Резервируем код за группой сразу — следующий объект той же группы продолжит с этого значения.
        _groupLastCodeCache[groupCacheKey] = newCode;

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

    private string ForObjectConstructString(PolynomObjectWithShortProperties model)
        => $"у объекта с objectId '{model.ObjectId}' и typeId '{model.TypeId}' " +
           $"с наименованием '{model.Name}'";
}
