# Процессор кодов классификатора

Детальное описание компонента, отвечающего за вычисление и установку кодов классификатора.

## Интерфейс

**Путь:** `NsiTransfer.BLL/Interfaces/Services/IClassificationCodeProcessor.cs`

```csharp
public interface IClassificationCodeProcessor
{
    Task<Result<PolynomObject>> ProcessAsync(
        PolynomObjectWithShortProperties mappedInOutputModel,
        Message currentMessage,
        CancellationToken cancellationToken);
}
```

**Входные параметры:**
- `mappedInOutputModel` — объект Полинома с кратким описанием
- `currentMessage` — сообщение синхронизации
- `cancellationToken` — токен отмены

**Возвращаемое значение:** `Result<PolynomObject>` — лог объекта в БД или ошибка

## Реализация

**Путь:** `NsiTransfer.BLL/Services/ClassificationCodeProcessor.cs`

### Основной алгоритм (ProcessAsync)

```
1. Получить свойство "Код классификатора" из объекта
   └─ Если свойство не найдено → ошибка CannotFindPropertyClassificationCode

2. Если код уже установлен (не пустой)
   └─ Вернуть лог с существующим кодом (exit)

3. Получить родительские группы объекта
   └─ Если ошибка API → ошибка ErrorWhileProcessWasExecuting

4. Проанализировать количество групп
   ├─ Если 0 групп → ошибка NoGroupsAtAll
   ├─ Если 1 группа → использовать её
   └─ Если >1 группы → выбрать группу с кодом классификатора

5. Если группа содержит минимальное/максимальное значение
   └─ Использовать эти значения (не вычислять)

6. Получить максимальный код из группы
   └─ GetLastClassificationCodeInGroup()

7. Вычислить новый код
   └─ IncrementCode(maxCode)

8. Установить код объекту
   └─ UpdateClassificationCodeAsync()

9. Сохранить лог в БД
   └─ CreatePolynomObjectLog()
```

## Определение "корректной" группы

**Логика выбора:**

```csharp
if (parentGroups.Count == 0)
    // Ошибка: нет групп
else if (parentGroups.Count == 1)
    // Используем единственную группу
    correctGroup = parentGroups[0];
else
    // Ищем группу с свойством "Код классификатора"
    correctGroup = parentGroups.SingleOrDefault(HasClassificationCodeProperty);
    // Если найдено несколько → ошибка MultipleGroupsWithMinMax
```

## Вычисление кода

**Метод:** `GetLastClassificationCodeInGroup()`

```
1. Инициализировать переменную result = null
2. Пагинировать дочерние узлы группы (по 100 за раз)
   └─ GetClassificationNodeChildren(pageNumber++, pageSize=100)

3. Для каждого дочернего узла:
   ├─ Получить свойства GetAllPropertiesOfObject()
   ├─ Найти значение "Код классификатора"
   ├─ Проверить, что код входит в диапазон [minValue, maxValue] группы
   │   └─ Если код вне диапазона → пропустить (ошибочные данные, не участвуют в вычислении максимума)
   └─ Сравнить с текущим максимальным (NumericStringComparer)
       └─ Если больше → обновить result

4. Продолжить до конца пагинации (HasNextPage = false)

5. Вернуть максимальный код (или null, если валидных кодов не найдено — тогда стартуем от minValue)
```

**Сигнатура (актуальная):**

```csharp
Task<Result<string?>> GetLastClassificationCodeInGroup(
    int groupObjectId, IdentifiableObjectType groupTypeId,
    string minValue, string maxValue,
    CancellationToken cancellationToken);
```

Границы `minValue`/`maxValue` передаются вызывающим кодом (`ClassificationCodeProcessor.HandleOneGroupAsync`) — это уже проверенные `minProp.Value`/`maxProp.Value` группы.

**Компаратор:** `NumericStringComparer`

Сравнивает коды как числа, но обрабатывает строковый формат:
- "001" < "002" < "003" < "010"
- "999" < "1000"

## Обработка ошибок

**Тип:** `PolynomObjectFailureTypeEnum`

| Ошибка | Когда | Решение |
|--------|-------|---------|
| `CannotFindPropertyClassificationCode` | Нет свойства "Код классификатора" в концепции | Проверить конфигурацию в `configuration.json` |
| `NoGroupsAtAll` | Объект не входит ни в какую группу | Добавить объект в группу классификации |
| `MultipleGroupsWithMinMax` | Несколько групп с кодами | Удалить объект из лишних групп |
| `ErrorWhileProcessWasExecuting` | Ошибка API Полинома | Проверить подключение к API, логи |

## Сохранение результатов

### При успехе

**Таблица:** `PolynomObjects`

```csharp
new PolynomObject
{
    ObjectId = object.ObjectId,
    TypeId = object.TypeId,
    MessageId = message.Id,
    ClassificationCode = newCode,
    Status = ProcessingStatus.Success,
    ProcessedAt = DateTime.UtcNow
}
```

### При ошибке

**Таблица:** `PolynomObjectFailures`

```csharp
new PolynomObjectFailure
{
    ObjectId = object.ObjectId,
    TypeId = object.TypeId,
    MessageId = message.Id,
    FailureType = failureType,
    ErrorMessage = errorMessage,
    FailedAt = DateTime.UtcNow
}
```

## Конфигурация

**Файл:** `configuration.json`

```json
"PolynomApiSyncOptions": {
  "ConceptNameForClassificationData": "Данные классификатора",
  "ClassificationCodePropertyName": "Код классификатора",
  "OwnContractName": "Собственные свойства",
  "MinCodePropertyName": "Минимальное значение кода",
  "MaxCodePropertyName": "Максимальное значение кода"
}
```

**Использование:**
- `ConceptNameForClassificationData` — ищем эту концепцию у объекта
- `ClassificationCodePropertyName` — ищем это свойство в концепции
- `MinCodePropertyName` / `MaxCodePropertyName` — если есть, используем их вместо вычисления

## Инъекции зависимостей

**Регистрация:** `NsiTransfer.BLL/HostExtensions.cs`

```csharp
services.AddScoped<IClassificationCodeProcessor, ClassificationCodeProcessor>();
```

**Зависимости:**
- `IPolynomApiService` — работа с API
- `IUnitOfWork` — доступ к БД
- `IOptionsMonitor<PolynomApiSyncOptions>` — конфигурация
- `ILogger<ClassificationCodeProcessor>` — логирование

## Примеры использования

### Обработка одного объекта

```csharp
var processor = serviceProvider.GetRequiredService<IClassificationCodeProcessor>();

var result = await processor.ProcessAsync(
    mappedObject,
    message,
    cancellationToken);

if (result.IsSuccess)
{
    var log = result.Data; // PolynomObject
    Console.WriteLine($"Код: {log.ClassificationCode}");
}
else
{
    Console.WriteLine($"Ошибка: {result.ErrorMessage}");
}
```

### Массовая обработка

```csharp
var results = new List<Result<PolynomObject>>();

foreach (var obj in objects)
{
    var result = await processor.ProcessAsync(obj, message, cancellationToken);
    results.Add(result);
}

var successCount = results.Count(r => r.IsSuccess);
var failureCount = results.Count(r => !r.IsSuccess);
```

## Баги и исправления (August 2026)

### Проблема 1: Объекты в 2+ группах не попадали в брокер

**Причина:** Предикат `HasClassificationCodeProperty` при выборе группы (case > 1) проверял наличие **объектного** свойства "Код классификатора", а не Min/Max свойств группы. Для типовых объектов в 2+ группах этот предикат возвращал false для всех групп, что приводило к молчаливому падению в ошибку `NoGroupsWithMinMax`.

**Решение:** Заменена функция на `HasMinMaxProps()` (строка 290-294), которая проверяет наличие оба Min и Max свойств группы.

**Файлы:** `ClassificationCodeProcessor.cs:100, 290-294`

### Проблема 2: Коды, выходящие за диапазон группы, попадали в брокер

**Причина:** Объект с уже установленным ошибочным кодом (из предыдущей попытки синхронизации) использовался БЕЗ проверки диапазона (строка 59-61).

**Решение:** Добавлена проверка диапазона для уже установленных кодов (строки 64-95). Теперь все коды (новые и старые) проверяются перед отправкой в брокер.

**Файлы:** `ClassificationCodeProcessor.cs:64-95`

### Проблема 3: Диапазон кодов в группе переполнялся

**Причина:** При вычислении нового кода не было проверки на минимум — только на максимум. Код мог быть вычислен < minValue (хотя это редко).

**Решение:** Добавлена двусторонняя проверка: код >= minValue И <= maxValue (строки 184-188).

**Файлы:** `ClassificationCodeProcessor.cs:184-188`

### Проблема 4: Необработанные исключения ломали весь batch

**Причина:** Исключение "код превышает максимум" на строке 172 не было перехвачено, пробивало весь batch в `PendingSending.catch(Exception)`.

**Решение:** Добавлен try/catch вокруг `HandleOneGroupAsync()` (строки 162-174), исключение преобразуется в контролируемую ошибку объекта.

**Файлы:** `ClassificationCodeProcessor.cs:162-174`

### Проблема 5: Отсутствие логирования при исключении объектов

**Причина:** Объекты исключались из batch молча, без явного лога о причине.

**Решение:** 
- Добавлен лог в `SyncUseCases.cs:574-577` при исключении объекта из batch.
- Добавлено уведомление об ошибке через `IErrorNotifier` (строки 579-580).

**Файлы:** `SyncUseCases.cs:574-580`

### Проблема 6: Ошибочные коды вне диапазона группы влияли на вычисление максимума

**Причина:** `GetLastClassificationCodeInGroup()` сканировал коды всех дочерних объектов группы и брал максимум без учёта диапазона `[MinCodePropertyName, MaxCodePropertyName]` группы. Если среди детей был объект с ошибочным кодом (например, из старой синхронизации или установленным вручную вне диапазона), этот код становился "максимумом", и инкремент от него либо превышал `maxValue` (падение с `InvalidOperationException`, блокирующее обработку корректных объектов группы), либо давал некорректный результат.

**Решение:** В `GetLastClassificationCodeInGroup` добавлены параметры `minValue`/`maxValue` — каждый найденный код дочернего объекта теперь проверяется на попадание в диапазон группы (через `NumericStringComparer`) до сравнения с текущим максимумом; коды вне диапазона пропускаются как ошибочные данные. Постфактум-проверка результата (Проблема 3) осталась как есть — независимая защита от прочих аномалий. Если после фильтрации в группе не осталось валидных кодов, логика корректно стартует с `minValue` (та же ветка, что и для пустой группы).

**Файлы:** `IPolynomApiService.cs:25`, `PolynomApiService.cs:153-201`, `ClassificationCodeProcessor.cs:200`

### Проблема 7: Объекты вне всех групп справочника (CanUnassign=true) повторялись без конца

**Причина:** Объект может иметь понятие "Данные Классификатора", но при этом не входить ни в одну группу справочника Классификатора — Polynom API сигнализирует это через `CanUnassign=true` на контракте. При каждом retry-цикле такой объект попадал в `RetryFailedObjectsAsync`, повторно вызывался `ProcessAsync`, который вновь падал с ошибкой (`NoGroupsWithMinMax`). Цикл продолжался без диагностики причины оператору.

**Решение:** Добавлена проверка `CanUnassign` в `RetryFailedObjectsAsync` (перед вызовом `ProcessAsync`):
- Если контракт "Данные Классификатора" имеет `CanUnassign=true`:
  - Если код классификатора **пустой** → удалить запись из PolynomObjectFailures (объект теперь валиден)
  - Если код классификатора **не пустой** → оставить запись, отправить email оператору (объект осиротел вне групп, требует вмешательства), прервать retry на этом объекте

**Файлы:** `SyncUseCases.cs:763-796` (RetryFailedObjectsAsync), вставка после построения `objWithProps` и перед проверкой `IsConceptNameForClassificationDataExists`

### Проблема 8: Массовое создание объектов в группе — N полных обходов группы вместо одного

**Причина:** `HandleOneGroupAsync` вызывал `GetLastClassificationCodeInGroup()` отдельно для КАЖДОГО объекта. При массовом создании объектов в одной группе (например batch из 50 новых объектов одной группы классификатора) это давало 50 полных обходов группы — пагинация по 100/страница + запрос свойств на каждого child, на каждый обрабатываемый объект.

**Ключевой инсайт:** `ClassificationCodeProcessor` — Scoped, новый экземпляр на каждый sync run (новый DI-scope в `SyncUseCases.StartDataCollectionInBackgroundAsync`). Обработка объектов строго последовательная (foreach, без параллелизма) — и в главном цикле, и в retry-цикле. Значит кеш максимума кода группы можно держать простым `Dictionary`-полем на самом процессоре, БЕЗ блокировок — он естественно живёт один sync run и не требует thread-safety.

**Решение:** Добавлено поле `_groupLastCodeCache` (`Dictionary` по ключу `(GroupObjectId, GroupTypeId)`). `HandleOneGroupAsync` теперь берёт последний код из кеша, если группа уже обрабатывалась в этом run; иначе вызывает `GetLastClassificationCodeInGroup` один раз и сразу резервирует вычисленный `newCode` в кеше — ДО отправки в Полином (следующий объект той же группы видит зарезервированный код, а не старый максимум).

**Статус:** подтверждено пользователем, работает.

**Файлы:** `ClassificationCodeProcessor.cs:22-25` (поле кеша), `ClassificationCodeProcessor.cs:192-231` (HandleOneGroupAsync — чтение/резервирование кеша)

## Проверка кодов вне диапазона группы

Объекты с кодом классификатора вне диапазона [Min, Max] группы обрабатываются как ошибки и исключаются из брокера.

### Когда проверяется

**При наличии уже установленного кода** (`ClassificationCodeProcessor.cs`, строки 57-95):
1. Получить родительские группы объекта
2. Выбрать группу с Min/Max свойствами
3. Проверить диапазон
4. Если вне диапазона → ошибка `ClassificationCodeOutOfGroupRange`

### Обработка ошибки

1. Записывается в `PolynomObjectFailures` (с типом `ClassificationCodeOutOfGroupRange`)
2. Отправляется письмо об ошибке через `IErrorNotifier`
3. Объект **исключается из RabbitMQ** (не отправляется в очередь)
4. Message **не удаляется** — сохраняется для аудита

### Поведение в смешанном сценарии

Если в одном Message:
- Объект 1: код корректный → отправляется в RabbitMQ ✓
- Объект 2: код вне диапазона → PolynomObjectFailures + письмо об ошибке ✓

Message с успешными объектами публикуется в очередь. Проблемные объекты логируются отдельно.

### Если все объекты Message имеют ошибки

1. Message **не удаляется** (сохраняется для аудита)
2. Записывается в `MessageFailure` (уровень сообщения)
3. Ничего не отправляется в RabbitMQ
4. PolynomObjectFailures содержит детали каждой ошибки объекта

## Персистентный кеш последнего кода группы (сентябрь 2026)

**Проблема:** in-memory кеш `_groupLastCodeCache` (см. "Проблема 8" выше) живёт один sync run — на следующем запуске для той же большой группы снова требуется полный обход через Polynom API (~505 запросов на группу).

**Решение:** новая таблица `ClassificationGroupCodeMaxes` (Entity `ClassificationGroupCodeMax`), персистентно хранит последний выданный код по каждой группе.

**Сущность:** `NsiTransfer.DAL/Db/Entities/ClassificationGroupCodeMax.cs`

```csharp
public class ClassificationGroupCodeMax
{
    public long Id { get; set; }
    public int GroupObjectId { get; set; }
    public IdentifiableObjectType GroupTypeId { get; set; }
    public string? LastMaxCode { get; set; }  // null = группа пуста, старт с MinValue
    public DateTime UpdatedAt { get; set; }
}
```

Уникальный индекс на (GroupObjectId, GroupTypeId). Миграция: `20260907164713_AddClassificationGroupCodeMaxTable`.

**Чтение (`HandleOneGroupAsync`, `ClassificationCodeProcessor.cs`):** при промахе in-memory кеша сначала проверяется персистентная таблица (`_unitOfWork.ClassificationGroupCodeMaxes`). Если строка найдена — используется её `LastMaxCode` вместо полного обхода группы через `GetLastClassificationCodeInGroup`. Обход через API теперь происходит только для групп, у которых ещё нет строки в таблице (первое обращение после деплоя/для новой группы).

**Запись:** после вычисления `newCode` строка апсертится в `ClassificationGroupCodeMaxes` (без немедленного `SaveChangesAsync` — уезжает вместе с остальными изменениями текущего batch, как и `PolynomObjects`/`PolynomObjectFailures`).

**Известное ограничение (принятый риск, см. `architecture-improvements-plan.md`):** кеш не сверяется с реальным состоянием Polynom при каждом чтении. Если объект в группе создан вручную в Polynom в обход NsiTransfer с кодом БОЛЬШЕ закешированного значения — возможен конфликт (NsiTransfer выдаст уже занятый код). Если объекты удалялись — кеш просто не переиспользует освободившиеся номера (это отдельная задача, "Проблема 4" в плане развития). Восстановление актуальности — кнопка переиндексации.

**Переиндексация (кнопка):** `POST /api/polynom-sync/rebuild-group-code-cache` (`PolynomSyncController.cs`), требует `[Authorize]`. Отказывает (409), если сейчас идёт активная синхронизация (`ISyncUseCases.IsThereAlreadyActiveSync`). Работа выполняется в фоне через `IBackgroundTaskQueue` ("sync" очередь) — HTTP-ответ приходит сразу, результат смотреть в логах приложения (`ILogger<SyncUseCases>`, сообщения "Переиндексация кеша кодов классификатора...").

**Иерархия классификатора Polynom** (подтверждено пользователем + `docs/swagger.json`): Справочник (Reference) → Каталог (Catalog) → Группа (Group, может содержать вложенные подгруппы или элементы/объекты). `TargetReferenceNode` из конфига — это именно СПРАВОЧНИК (верхний уровень), не группа.

Алгоритм (`ClassificationCodeProcessor.RebuildGroupCodeCacheAsync`):
1. Стартовая точка — `TargetReferenceNode` из конфига (Справочник)
2. `GetCatalogsByReference` (`element-catalog/get-by-reference`) — каталоги справочника
3. Для каждого каталога — `GetGroupsByCatalog` (`element-group/get-by-catalog`) — группы верхнего уровня каталога
4. Для каждой группы верхнего уровня — рекурсивный обход `WalkAndIndexGroupAsync` через `GetSubGroups` (`element-group/get-by-group` — та же семантика, что `IsGroupLeafAsync` использует в обычном sync flow, возвращает ТОЛЬКО подгруппы, не элементы). Если подгрупп нет — узел конечная группа
5. Для конечной группы (`IndexOneGroupAsync`): получаем свойства через `GetAllPropertiesOfObject`, оборачиваем objectId/typeId/name в `NamedObject` (конкретный класс Ascon SDK, `Ascon.Polynom.Web.Api.Data.Models.Base.NamedObject`, реализует `INamedObject`) для `ModelMapper.CreateObjectWithShortProperties`, проверяем наличие Min/Max свойств группы (`GetMinMaxProps`). Если нет — это обычная папка-категория, не группа классификатора, пропускаем. Если есть — вычисляем последний код через `GetLastClassificationCodeInGroup` и апсертим в БД
6. `SaveChangesAsync` каждые 50 проиндексированных групп + финальный save

**Три раунда фикса по фидбеку с реальных прогонов (сентябрь 2026):**
1. Изначально искал `TargetReferenceNode` обходом от `GetClassificationRootNode()` → первый слой через `GetClassificationChildrenNodes` — "не найден среди узлов первого уровня" (справочник не discoverable так через дерево классификации)
2. После точечного фикса (брать детей по objectId/typeId напрямую) обход проваливался в реальные объекты/товары (`property-owner/get-properties` возвращал 500) — `GetClassificationChildrenNodes`/`GetClassificationNodeChildren` возвращает узлы дерева ЦЕЛИКОМ (и подгруппы, и элементы), не только подгруппы. Заменено на `element-group/get-by-group` (`GetSubGroups`) — та же семантика, что уже использует проверенный в проде `IsGroupLeafAsync`
3. Это исправило обход ВНУТРИ группы, но `element-group/get-by-group` на objectId самого справочника (Reference) вернул 404 ("Группа элементов не найдена") — справочник не является ни группой, ни каталогом. Добавлены `GetCatalogsByReference`/`GetGroupsByCatalog` (`element-catalog/get-by-reference`, `element-group/get-by-catalog`) для правильного спуска Справочник → Каталог → Группа, дальше уже `GetSubGroups`

Точный состав интерфейсов Ascon SDK (`ElementGroup`/`ElementCatalog : INamedObject`, конкретный класс `NamedObject`, все запросы принимают `IIdentifierRequest`) подтверждён декомпиляцией `libs/AsconLibs-05-06-26/Ascon.Polynom.Web.Api.Data.dll` (`ilspycmd`) и сверкой с `docs/swagger.json` (Polynom API), не угадыванием.

**Известные ограничения обхода:**
- `GetGroupsInsideElementGroup`/`GetGroupsByCatalog`/`GetCatalogsByReference` не пагинируются в текущей реализации (как и уже существующий `IsGroupLeafAsync`) — если на одном уровне аномально много прямых детей, возможна неполная выборка. Такой же риск уже принят существующим кодом до этой задачи, не специфичен для переиндексации
- Максимальная глубина рекурсии внутри `WalkAndIndexGroupAsync` — 20 уровней (защита от зацикливания/некорректных данных)

**Файлы:**
- `ClassificationGroupCodeMax.cs` (новая сущность)
- `AppDbContext.cs` (DbSet + fluent config)
- `IUnitOfWork.cs`, `UnitOfWork.cs` (репозиторий)
- `ClassificationCodeProcessor.cs` (чтение/запись кеша в `HandleOneGroupAsync`, `UpsertGroupCodeMaxAsync`, `RebuildGroupCodeCacheAsync`, `WalkAndIndexGroupAsync`, `IndexOneGroupAsync`)
- `IClassificationCodeProcessor.cs` (сигнатура `RebuildGroupCodeCacheAsync`)
- `GroupCodeCacheRebuildResult.cs` (DTO итога переиндексации, `NsiTransfer.Contract/Models/DTO/`)
- `ISyncUseCases.cs`, `SyncUseCases.cs` (`RebuildGroupCodeCacheInBackgroundAsync` — guard на активную синхронизацию + постановка в фоновую очередь)
- `PolynomSyncController.cs` (`POST /api/polynom-sync/rebuild-group-code-cache`)
- `IPolynomApiService.cs`/`PolynomApiService.cs` — `GetSubGroups`, `GetCatalogsByReference`, `GetGroupsByCatalog`
- `IPolynomApiHttpRepository.cs`/`PolynomApiHttpRepository.cs` — `GetElementCatalogsByReference`, `GetElementGroupsByCatalog` (низкоуровневые HTTP-вызовы)
- `ApiRoutes.cs` — `GetElementCatalogsByReference` (`element-catalog/get-by-reference`), `GetElementGroupsByCatalog` (`element-group/get-by-catalog`)
- Миграция: `Migrations/20260907164713_AddClassificationGroupCodeMaxTable.cs`

**Статус:** реализовано 2026-09-07, три раунда фикса по фидбеку с реальных прогонов (см. выше), билд проходит без ошибок. Дальнейшая функциональная проверка — на пользователе.

## Переиспользование свободных номеров после удаления (сентябрь 2026)

**Проблема:** объекты могут удаляться из середины группы — освобождаются номера кода. Старая логика: код всегда растёт от максимума, при `newCode > maxValue` — сразу ошибка, свободные номера не переиспользуются.

**Решение (Lazy Search, этап 1 плана):** обычный путь (increment) не меняется. Когда вычисленный код превышает `maxValue` группы — вместо немедленной ошибки запускается полный обход группы (`GetAllClassificationCodesInGroup`, новый метод в `IPolynomApiService`, зеркалит `GetLastClassificationCodeInGroup`, но собирает ВСЕ занятые коды в `HashSet<string>`, а не только максимум), вычисляются все свободные номера в диапазоне (`ComputeFreeCodesQueue`) и складываются в очередь `_groupFreeCodesCache` (Dictionary по ключу группы, живёт один sync run — как и `_groupLastCodeCache`).

**Режим "группа исчерпана":** как только группа попала в `_groupFreeCodesCache`, ВСЕ последующие объекты этой группы в текущем run берут код из очереди (`Dequeue`), минуя обычный increment-путь и персистентный кеш из "Проблемы 3" полностью. Это принципиально: `_groupLastCodeCache`/БД `LastMaxCode` для исчерпанной группы НЕ обновляется найденным свободным номером — иначе следующий lookup стартовал бы инкремент от маленького gap-значения и столкнулся бы с уже занятыми кодами выше по диапазону. "Последний максимум" остаётся зафиксирован на `maxValue`; на следующий sync run группа снова определяется как исчерпанная и снова запускает полный обход (осознанный компромисс — см. "Известные ограничения" ниже).

**Защита от труда впустую:** `MaxFreeCodeScanIterations = 500_000` — лимит итераций в `ComputeFreeCodesQueue`. При превышении сканирование прерывается, но результат (частичная очередь) помечается флагом `Truncated = true`, который прокидывается до места, где очередь пустеет — тогда сообщение об ошибке явно говорит "поиск прерван по лимиту итераций", а не вводит в заблуждение "свободных номеров не осталось". Также пишется `_logger.LogWarning` сразу при построении усечённой очереди. **Найдено и исправлено ревью-workflow** (изначальная версия молча возвращала неполную очередь без индикации — при огромном разреженном диапазоне это давало ложную "нет свободных номеров", хотя они были).

**Известные ограничения (принятые компромиссы):**
- Персистентный кеш (Проблема 3) не хранит "флаг исчерпания" группы — при следующем sync run исчерпанная группа снова триггерит полный обход через API (~505 запросов), даже если по факту свободные номера всё ещё есть. Это этап 1 плана (Lazy Search); полная интеграция с персистентным кешем (этап 2 — сохранять найденный свободный номер в БД) не реализована в этом проходе, оставлена на будущее при необходимости.
- `ComputeFreeCodesQueue` повторно фильтрует коды по диапазону, хотя `GetAllClassificationCodesInGroup` уже гарантирует их принадлежность диапазону — минорная избыточность (не баг, оставлено как defensive-код).

**Файлы:**
- `IPolynomApiService.cs`, `PolynomApiService.cs` — `GetAllClassificationCodesInGroup`
- `ClassificationCodeProcessor.cs` — `_groupFreeCodesCache`, `HandleOneGroupAsync` (режим исчерпанной группы), `BuildFreeCodesQueueAsync`, `ComputeFreeCodesQueue`, `BuildFreeCodesExhaustedMessage`

**Статус:** реализовано 2026-09-07, adversarial review (3 lens'а × verify, haiku) нашёл и подтвердил баг с молчаливым усечением — исправлен. Билд проходит без ошибок.

## Информация о группе в JSON объекта (сентябрь 2026)

**Проблема:** JSON объекта, отправляемый в RabbitMQ, не содержал данных о группе классификатора — получатель не мог однозначно определить место объекта в справочнике без дополнительных запросов к Polynom API.

**Решение:** новое поле `GroupInfo? GroupInfo` в `PolynomObjectWithShortProperties` (`NsiTransfer.Contract/Models/DTO/`). Новый класс `GroupInfo { ObjectId, TypeId, Name, MinCode, MaxCode, IsLeaf }` (`GroupInfo.cs`).

**Заполнение — без дополнительных запросов к Polynom API:** к моменту, когда в `ClassificationCodeProcessor.ProcessAsync` определена корректная родительская группа объекта (`correctGroup`/`correctGroupForNewCode`), её данные (включая Min/Max свойства) уже получены через `GetParentGroupsWithProperties`, а `IsLeaf` уже подтверждён через `EnsureGroupIsLeafAsync` — `BuildGroupInfo` просто собирает `GroupInfo` из уже имеющихся данных, обе точки: путь для объекта с уже существующим кодом (после `EnsureGroupIsLeafAsync`, строка ~101) и путь вычисления нового кода (после `EnsureGroupIsLeafAsync`, перед `HandleOneGroupAsync`).

**Механизм передачи в JSON:** `mappedInOutputModel` — тот же самый объект (по ссылке), что уже лежит в списке `objectsWithProperties`/`retryObjectsWithProperties` в `SyncUseCases.cs` до вызова `ProcessAsync`; мутация `.GroupInfo` внутри процессора автоматически попадает в `JsonSerializer.Serialize(objectsWithProperties, ...)` — как и установка кода классификатора (`prop.Value = ...`), никаких изменений в `SyncUseCases.cs`/`RabbitMqPublisher.cs` не потребовалось.

**Известное ограничение (подтверждено adversarial review, намеренное поведение, не баг):** для объекта с УЖЕ существующим кодом классификатора (путь без вычисления нового кода) `GroupInfo` остаётся `null`, если: (а) `GetParentGroupsWithProperties` вернул ошибку или пустой список, или (б) среди родительских групп ни одна не имеет Min/Max свойств. В этих случаях объект всё равно считается успешно обработанным (существующий код не проверяется на диапазон) и отправляется в брокер — но без `GroupInfo`. Это осознанная асимметрия с путём вычисления НОВОГО кода, где такие состояния — жёсткая ошибка (`NoGroupsAtAll`/`NoGroupsWithMinMax`), не тихий null. Задокументировано в docstring `PolynomObjectWithShortProperties.GroupInfo`.

**Файлы:**
- `GroupInfo.cs`, `PolynomObjectWithShortProperties.cs` (новое поле)
- `ClassificationCodeProcessor.cs` — `BuildGroupInfo`, установка в обоих путях `ProcessAsync`

**Статус:** реализовано 2026-09-07, adversarial review (2 lens'а × verify, haiku) подтвердил корректность совпадения GroupInfo с группой валидации и нашёл описанное выше ограничение (принято как есть). Билд проходит без ошибок.

## Связанные страницы

- [[classification]] — Система классификации
- [[classification-api]] — API методы
- [[polynom-api]] — Полином API
- [[sync-flow]] — Процесс синхронизации

