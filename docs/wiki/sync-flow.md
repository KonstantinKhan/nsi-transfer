# Процесс синхронизации данных

Полный описание потока синхронизации от запуска до завершения.

## Инициирование синхронизации

### BackgroundService (Периодическая синхронизация)

**Класс:** `PolynomApiSyncBackgroundService`

**Механизм:** Самописный `BackgroundService` (ASP.NET Core hosted service), запускает синхронизацию по интервалу через `Task.Delay`.

**Интервал:** `StartSyncWithIntervalMinutes` из `PolynomApiSyncOptions` (default: 15 минут, конфигурируется в `configuration.json`).

**Регистрация:** `Program.cs` → `builder.Services.AddHostedService<PolynomApiSyncBackgroundService>()` (убедитесь, что не закомментирована).

**Логика цикла:**
1. Проверить, что конфиг валиден (TargetReferenceNode, RabbitMqQueues, RabbitMqRetryParams, PolynomApiSyncOptions, EmailNotifications.ErrorRecipients)
2. Если конфиг невалиден — ждать сигнала об изменении файла конфига, sync не запускается
3. Проверить, что нет уже активной синхронизации (ActiveMarker в таблице Sendings)
4. Если активная синхронизация есть — пропустить tick с логированием warning
5. Если всё OK — вызвать `SyncUseCases.StartDataCollectionAndWaitAsync()`
6. Вычислить оставшееся время до следующего интервала = StartSyncWithIntervalMinutes - elapsed time
7. `Task.Delay` на оставшееся время (или immediately если run занял > интервала)

**Обработка ошибок:** Необработанные исключения ловятся в top-level catch, логируются и отправляется email-уведомление. Цикл продолжается (сервис не падает).

**Note (2026-08-26):** BackgroundService был отключен (закомментирована регистрация в Program.cs). Re-enabled для возобновления периодического sync. Убедитесь, что конфиг на среде содержит валидные значения.

### REST API запрос

**Метод:** `POST /api/polynom-sync/start-data-collection-in-background`

**Тело запроса:**

```json
{
  "initiatorName": "admin"
}
```

**Ответ (200 OK):**

```json
{
  "SendingId": "550e8400-e29b-41d4-a716-446655440000",
  "Status": "InProgress",
  "StartedAt": "2026-08-25T12:00:00Z",
  "Message": "Sync started"
}
```

## Контроллер

**Класс:** `PolynomSyncController.StartDataCollectionInBackgroundAsync()`

**Логика:**

```csharp
1. Проверить, нет ли уже активной синхронизации
   └─ IsThereAlreadyActiveSync()

2. Если активна → вернуть 409 Conflict

3. Если свободно → запустить фоновый процесс
   └─ _syncUseCases.StartDataCollectionInBackgroundAsync()

4. Вернуть 200 OK с SendingId

// Фоновый процесс продолжает выполняться
```

## Use Cases (бизнес-логика)

**Класс:** `SyncUseCases.StartDataCollectionInBackgroundAsync()`

**Процесс:**

```
1. Определить время последней синхронизации
   └─ GetLastSuccessfulSyncTime()

2. Вычислить временной диапазон
   ├─ From: последняя успешная синхронизация
   └─ To: текущее время

3. Получить изменённые объекты за период
   └─ IPolynomApiService.GetDiffsInTimePeriod()
       └─ POST /api/v1/search/execute-property-search

4. Для каждого объекта:
   ├─ Создать модель PolynomObjectWithShortProperties
   ├─ Вызвать ClassificationCodeProcessor.ProcessAsync()
   │   └─ Вычислить и установить код классификатора
   └─ Сохранить результат в БД

5. Опубликовать событие в RabbitMQ
   ├─ Exchange: nsitransfer.exchange
   └─ Queue: polynom.search.results

6. Обновить время последней синхронизации
   └─ SetLastSuccessfulSyncTime(DateTime.UtcNow)
```

## Поиск изменённых объектов

**Метод:** `IPolynomApiService.GetDiffsInTimePeriod()`

**Запрос к API:**

```
POST /api/v1/search/execute-property-search

{
  "OwnerScope": {
    "ObjectId": 61,  // Из configuration.json
    "TypeId": 48
  },
  "Condition": {
    // Фильтр: дата изменения между From и To
    "SimpleConditions": [
      {
        "Definition": "@DateModified",
        "Operation": 6,  // GreaterThanOrEqual
        "Value": {/* From */}
      },
      {
        "Definition": "@DateModified",
        "Operation": 4,  // LessThan
        "Value": {/* To */}
      }
    ]
  },
  "PageNumber": 1,
  "PageSize": 100
}
```

**Ответ:**

```json
{
  "Items": [
    {
      "Object": {"ObjectId": 123, "TypeId": 45},
      "Name": "Номенклатура 1",
      "Properties": [/* свойства */]
    }
  ],
  "HasNextPage": true,
  "PageNumber": 1,
  "PageSize": 100
}
```

## Обработка классификации

**Для каждого объекта из результата поиска:**

1. **Проверить, есть ли уже код**
   - Если да → проверить, что код находится в диапазоне [Min, Max] группы
   - Если нет → перейти к пункту 2

2. **Получить родительские группы**
   ```
   POST /api/v1/classification-object/get-parent-groups
   ```

3. **Получить свойства каждой группы**
   ```
   POST /api/v1/property-owner/get-properties
   ```

4. **Определить группу для кода**
   - Если 0 групп → ошибка NoGroupsAtAll
   - Если 1 группа → использовать её
   - Если >1 группы → выбрать группу с Min/Max свойствами
     - Если ровно 1 группа с Min/Max → использовать
     - Если >1 группы с Min/Max → ошибка MultipleGroupsWithMinMax (неоднозначность)
     - Если нет групп с Min/Max → ошибка NoGroupsWithMinMax

5. **Проверить диапазон кода в группе**
   - Если код < Min или код > Max → ошибка ErrorWhileProcessWasExecuting
   - Иначе → продолжить

6. **Получить максимальный код в группе**
   ```
   POST /api/v1/tree/get-classification-node-children
   (пагинировано)
   ```

7. **Вычислить новый код**
   - newCode = max(lastCode).Increment() или Min (если нет кодов)
   - Проверить: Min <= newCode <= Max
   - Если вне диапазона → ошибка ErrorWhileProcessWasExecuting

8. **Установить новый код объекту**
   ```
   POST /api/v1/property-owner/set-property-values
   ```

## Сохранение результатов

**База данных:** PostgreSQL

### При успехе

**Таблица:** `PolynomObjects`

```sql
INSERT INTO PolynomObjects 
  (ObjectId, TypeId, MessageId, ClassificationCode, Status, ProcessedAt)
VALUES 
  (123, 45, 550e8400-e29b-41d4-a716-446655440000, '004', 'Success', NOW())
```

**Параллельно:** Если объект был ошибочным в PolynomObjectFailures → старая запись удаляется

```sql
DELETE FROM PolynomObjectFailures 
WHERE ObjectId = 123 AND TypeId = 45
```

### При ошибке

**Таблица:** `PolynomObjectFailures`

```sql
INSERT INTO PolynomObjectFailures 
  (ObjectId, TypeId, MessageId, FailureType, ErrorMessage, FailedAt)
VALUES 
  (123, 45, 550e8400-e29b-41d4-a716-446655440000, 'NoGroupsAtAll', 'No groups found', NOW())
```

**Предварительно:** Если объект уже имел записи об ошибках → все они удаляются (переписываются новой ошибкой)

## Публикация в RabbitMQ

**После обработки всех объектов:**

```csharp
await _messagePublisher.PublishAsync(
    exchange: "nsitransfer.exchange",
    queue: "polynom.search.results",
    message: new SyncResultMessage
    {
        SendingId = sendingId,
        ProcessedCount = successCount,
        FailureCount = failureCount,
        CompletedAt = DateTime.UtcNow
    },
    cancellationToken);
```

**Параметры переподключения:**
- MaxRetryAttempts: 3
- RetryIntervalInSeconds: 10
- ConfirmationTimeOutInSeconds: 30

## Логирование

**Уровни логирования:**

```
INFO  - [API] → POST /api/v1/... | MethodName
INFO  - [API] REQUEST BODY: {...}
INFO  - [API] RESPONSE BODY: {...}
INFO  - [API] ✓ MethodName | /api/v1/... | Статус: 200 OK
ERROR - [API] ✗ MethodName | /api/v1/... | Статус: 500 | Ошибка: {...}
```

**Размер логов:**
- REQUEST BODY — полный JSON запроса
- RESPONSE BODY — первые 3000 символов ответа

## Мониторинг через SSE

**Эндпоинт:** `GET /api/polynom-sync/listen-for-sync-events?sendingId={id}`

**События:**

```
event: Started
data: {"SendingId": "...", "Message": "Sync started"}

event: ObjectProcessed
data: {"ObjectId": 123, "Code": "004"}

event: ObjectFailed
data: {"ObjectId": 123, "Error": "..."}

event: Completed
data: {"SendingId": "...", "TotalProcessed": 100}
```

## Параметры синхронизации и классификации (PolynomApiSyncOptions)

**Из configuration.json:**

```json
"PolynomApiSyncOptions": {
  "StartSyncWithIntervalMinutes": 15,
  "ConceptNameForClassificationData": "Данные классификатора",
  "ClassificationCodePropertyName": "Код классификатора",
  "OwnContractName": "Код по справочнику",
  "MinCodePropertyName": "МинКод",
  "MaxCodePropertyName": "МаксКод"
}
```

### Использование в синхронизации

- **StartSyncWithIntervalMinutes (15)**: BackgroundService запускает синхронизацию каждые 15 минут
- **ConceptNameForClassificationData**: Используется при вызове `GetPropertiesOfObject()` — фильтрует свойства по названию концепции "Данные классификатора"
- **ClassificationCodePropertyName**: Название свойства, где хранится/устанавливается код классификатора объекта
- **OwnContractName**: Используется в `GetParentGroupsWithProperties()` для поиска группы справочника "Код по справочнику"
- **MinCodePropertyName** и **MaxCodePropertyName**: Используются ClassificationCodeProcessor для чтения границ диапазона кодов из свойств группы и валидации: `min <= newCode <= max`

### Жизненный цикл

1. BackgroundService вызывает `SyncUseCases.StartDataCollectionInBackgroundAsync()` каждые N минут (N = StartSyncWithIntervalMinutes)
2. Для каждого найденного объекта `ClassificationCodeProcessor` использует остальные 5 параметров для определения кода
3. Параметры передаются через `IOptionsMonitor<PolynomApiSyncOptions>` и никогда не меняются во время работы синхронизации

## Обработка ошибок

### Ошибки API Полинома

Если API недоступен:
1. Логируется ошибка
2. Создаётся запись `PolynomObjectFailure`
3. Синхронизация продолжает обработку других объектов
4. Отправляется событие об ошибке в RabbitMQ

### Ошибки БД

Если БД недоступна:
1. Логируется критическая ошибка
2. Отправляется событие об ошибке синхронизации
3. Синхронизация останавливается

## Управление ошибочными объектами (PolynomObjectFailures)

### Основной цикл синхронизации (CollectObjectsAndSend)

Для каждого успешно обработанного объекта:
1. Объект добавляется в `PolynomObjects`
2. **Важно:** Если объект был в таблице `PolynomObjectFailures` → старая запись удаляется
3. Это происходит в двух случаях:
   - Объект без понятия классификатора (добавляется без обработки)
   - Объект успешно обработан ClassificationCodeProcessor

### Повторная обработка ошибочных объектов (RetryFailedObjectsAsync)

Запускается **после основного цикла** (если он не упал критически):

**Этапы:**

1. **Получить успешные объекты текущего Sending**
   ```
   SELECT PolynomObjectId, PolynomTypeId FROM PolynomObjects 
   WHERE MessageId IN (SELECT Id FROM Messages WHERE SendingId = current_sending_id)
   ```

2. **Получить ошибки из предыдущих Sending**
   ```
   SELECT * FROM PolynomObjectFailures 
   WHERE MessageId IN (SELECT Id FROM Messages WHERE SendingId != current_sending_id)
   ```

3. **Фильтрация:** Оставить только ошибки, которых нет в успешных объектах текущего Sending

4. **Для каждой ошибки:**
   - Запросить свойства объекта у API
   - Если свойства получены:
     - **Проверка CanUnassign** (Aug 27, 2026): если объект имеет понятие классификатора, но **CanUnassign=true** (не входит ни в одну группу справочника):
       - Если код классификатора пустой → удалить ошибку (объект валиден)
       - Если код классификатора не пустой → оставить ошибку, отправить email оператору (объект осиротел вне групп, требует вмешательства), не вызывать ProcessAsync
     - Если понятие классификатора исчезло → добавить в PolynomObjects, удалить ошибку
     - Если понятие есть и CanUnassign=false → повторить ProcessAsync
       - **Успех:** Удалить старую ошибку, добавить в PolynomObjects
       - **Неудача:** ClassificationCodeProcessor удалит старую ошибку и создаст новую

5. **Сохранить изменения:** `SaveChangesAsync()`

### Жизненный цикл записи PolynomObjectFailure

```
[Ошибка в обработке]
        ↓
[Запись добавляется в PolynomObjectFailures]
        ↓
[Следующая синхронизация]
   ├─ Если объект исправлен и попал в GetDiffsInTimePeriod (основной цикл):
   │  └─ Удалено из PolynomObjectFailures при добавлении в PolynomObjects
   │
   └─ Если объект исправлен но не в новых данных (остаётся в ошибках):
      └─ RetryFailedObjectsAsync повторит его обработку
         ├─ Успех → Удалено из PolynomObjectFailures
         └─ Неудача → Перезаписано новой ошибкой
```

**Важно:** Объект не может одновременно находиться в `PolynomObjects` и `PolynomObjectFailures` для одной пары (ObjectId, TypeId). При успехе ошибка всегда удаляется.

## Связанные страницы

- [[architecture]] — Архитектура приложения
- [[classification-code-processor]] — Обработчик кодов
- [[polynom-api]] — API Полинома
- [[rabbitmq]] — Брокер сообщений
- [[http-logging]] — Логирование запросов

