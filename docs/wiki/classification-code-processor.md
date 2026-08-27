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

## Связанные страницы

- [[classification]] — Система классификации
- [[classification-api]] — API методы
- [[polynom-api]] — Полином API
- [[sync-flow]] — Процесс синхронизации

