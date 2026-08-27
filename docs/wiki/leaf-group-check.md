# Проверка "объект в конечной группе" и страховка от удалённых объектов

**Дата:** 27.08.2026  
**Компоненты:** ClassificationCodeProcessor, SyncUseCases, PolynomApiService

## Проблема 1: Группа содержит подгруппы

При обработке объекта классификатора группа, в которой он находится, не должна содержать внутри себя другие группы (подгруппы). Объект обязан лежать в **конечной (терминальной) группе**.

### Решение

Добавлена проверка в `ClassificationCodeProcessor.ProcessAsync()` перед обработкой кода:

**Новый метод:** `EnsureGroupIsLeafAsync()`
- Запрашивает подгруппы через API: `POST /api/v1/element-group/get-by-group` (IIdentifierRequest)
- Если подгруппы найдены → failure с типом `GroupIsNotLeaf = 104`
- Если ошибка API → failure с типом `ErrorWhileProcessWasExecuting`
- Если группа терминальная → `null`, обработка продолжается

**Вызывается в двух местах `ProcessAsync()`:**
1. Когда код уже существует у объекта (строка ~76)
2. Когда код нужно вычислить (строка ~164)

**Слои обработки** (автоматически):
- Объект исключается из сообщения брокера (в `SyncUseCases`)
- Запись добавляется в `PolynomObjectFailures`
- Email-уведомление отправляется оператору

### Код

```csharp
// IPolynomApiService
Task<Result<bool>> IsGroupLeafAsync(int objectId, IdentifiableObjectType typeId, CancellationToken cancellationToken);

// PolynomApiService.cs:152-157
public Task<Result<bool>> IsGroupLeafAsync(int objectId, IdentifiableObjectType typeId, CancellationToken cancellationToken)
{
    var request = new IdentifierRequest { ObjectId = objectId, TypeId = typeId };
    return _apiRepository.GetGroupsInsideElementGroup(request, cancellationToken)
        .OnFailureAsync(err => err.AddError($"Не удалось проверить наличие подгрупп у группы objectId '{objectId}' typeId '{typeId}'."))
        .MapAsync(subGroups => subGroups.Count == 0);
}
```

### Enum

```csharp
[Description("Группа объекта не является конечной (содержит вложенные подгруппы)")]
GroupIsNotLeaf = 104
```

## Проблема 2: Удалённые объекты отправляются в брокер при ретрае

Если объект был удалён из Полинома, но остался в `PolynomObjectFailures`, при ретрае:
- API вернёт 200 OK с пустыми данными (`"allContracts":[]`)
- Объект воспринимается как успешный и отправляется в брокер
- Другие системы получают недостоверные данные об объекте, который уже не существует

### Решение

В `SyncUseCases.RetryFailedObjectsAsync()` добавлены две проверки (после получения свойств):

**1. Объект вернул 404:**
```csharp
if (propsResult.ErrorMessage?.Contains("Статус: 404") == true)
{
    _unitOfWork.PolynomObjectFailures.Delete(failure);
    await _unitOfWork.SaveChangesAsync(cancellationToken);
    continue;
}
```

**2. Объект вернул пустые контракты (200 OK но без данных):**
```csharp
if (propsResult.Data?.AllContracts == null || propsResult.Data.AllContracts.Count == 0)
{
    _unitOfWork.PolynomObjectFailures.Delete(failure);
    await _unitOfWork.SaveChangesAsync(cancellationToken);
    continue;
}
```

Вторая проверка критична, так как Polynom API может вернуть 200 OK даже если объект фактически удалён/повреждён — в этом случае контракты будут пусты.

### Логирование

Добавлено логирование для обоих случаев:
- 404: `"Object no longer exists in Polynom (404)"`
- Пустые данные: `"Object has no contracts/properties (likely deleted)"`

## Файлы изменений

- `NsiTransfer.DAL/Routes/ApiRoutes.cs` — маршрут `/element-group/get-by-group`
- `NsiTransfer.DAL/Interfaces/Http/IPolynomApiHttpRepository.cs` + реализация
- `NsiTransfer.BLL/Interfaces/Services/IPolynomApiService.cs` + реализация
- `NsiTransfer.Contract/Models/Enums/PolynomObjectFailureTypeEnum.cs` — добавлен `GroupIsNotLeaf`
- `NsiTransfer.BLL/Services/ClassificationCodeProcessor.cs` — метод `EnsureGroupIsLeafAsync()`, две точки вызова
- `NsiTransfer.BLL/UseCases/SyncUseCases.cs` — проверки в `RetryFailedObjectsAsync()`

## Гарантии

1. **Конечная группа:** перед отправкой в брокер объект проверяется, что его группа не содержит подгрупп
2. **Удалённые объекты:** при ретрае удалённые/пустые объекты удаляются из `PolynomObjectFailures` и не отправляются в брокер
3. **Автоматическая обработка:** обе проверки интегрированы в существующий механизм обработки ошибок
4. **Страховка от временных ошибок:** различаются 404 (объект не найден), пустые данные (объект повреждён) и иные ошибки API (могут повториться при ретрае)
