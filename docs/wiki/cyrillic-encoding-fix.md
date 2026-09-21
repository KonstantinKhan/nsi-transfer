# Cyrillic Encoding Fix in RabbitMQ Messages

**Дата:** 31 августа 2026  
**Статус:** Fixed  
**Файлы:** `SyncUseCases.cs`, `RabbitMqPublisher.cs`

## Проблема

Сообщения, отправляемые в RabbitMQ, содержали экранированные Unicode последовательности вместо читаемого кириллического текста:

**Current (broken):**
```json
{
  "Name": "Наименование",
  "Value": "Чугун литейный Л5"
}
```

**Expected:**
```json
{
  "Name": "Наименование",
  "Value": "Чугун литейный Л5"
}
```

Результат: в RabbitMQ UI и логах тексты отображаются как экранированные последовательности, трудно читать и отлаживать.

## Причина

`JsonSerializer.Serialize()` использует энкодер по умолчанию, который экранирует все не-ASCII символы (Cyrillic, Chinese, etc.) в виде `\uXXXX` последовательностей. Это валидный JSON, но не user-friendly.

Затронуты две точки сериализации в `SyncUseCases.cs`:
- Строка ~655: сериализация текущих объектов (`objectsWithProperties`)
- Строка ~866: сериализация retry объектов (`retryObjectsWithProperties`)

```csharp
// Было:
currentMessage.SerializedMessage = JsonSerializer.Serialize(objectsWithProperties);

// Результат: Unicode escape sequences
```

## Решение

Создан `MessageJsonOptions` с `UnsafeRelaxedJsonEscaping` энкодером — позволяет сохранять UTF-8 текст как есть:

```csharp
private static readonly JsonSerializerOptions MessageJsonOptions = new()
{
    Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
};
```

Оба вызова `Serialize` обновлены:

```csharp
// Теперь:
currentMessage.SerializedMessage = JsonSerializer.Serialize(objectsWithProperties, MessageJsonOptions);
retryMessage.SerializedMessage = JsonSerializer.Serialize(retryObjectsWithProperties, MessageJsonOptions);
```

**Результат:** Сообщения теперь содержат читаемый UTF-8 текст (Наименование, Чугун литейный Л5, и т.д.)

## Диагностика

Добавлены логи для проверки содержимого сообщений в **SyncUseCases.cs** (после сериализации):

```csharp
_logger.LogInformation("[MESSAGE SERIALIZED] Current message preview (first 1500 chars): {MessagePreview}", ...);
_logger.LogInformation("[MESSAGE SERIALIZED] Retry message preview (first 1500 chars): {MessagePreview}", ...);
```

Проверить консоль во время синхронизации — увидеть, содержит ли сообщение кириллицу или escape sequences.

## Техничес детали

- `UnsafeRelaxedJsonEscaping` — "unsafe" в имени означает, что разрешает более широкий диапазон символов (по сравнению с `JavaScriptEncoder.Default`). Для JSON и RabbitMQ абсолютно безопасно.
- Не использована `WriteIndented: true` — не нужна для production сообщений (уменьшает размер)
- Логирование ограничено 1500 символами — предотвращает flooding консоли в production

## Коммиты

1. **26e55a3** - fix: Исправление кодировки кириллических символов в сообщениях RabbitMQ
2. **200ab21** - debug: Добавлено логирование сообщений перед отправкой в RabbitMQ

## Проверка

Запустить синхронизацию и проверить:
1. Консоль должна показывать `[MESSAGE SERIALIZED]` и `[RABBIT_MQ_PUBLISH]` с читаемым текстом
2. RabbitMQ UI должен показывать кириллицу в JSON, а не escape sequences
3. Логи и UI должны быть понятнее при отладке
