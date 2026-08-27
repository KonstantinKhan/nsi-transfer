# RabbitMQ архитектура приложения NsiTransfer

## Обзор

Архитектура работы с RabbitMQ разделена по слоям приложения:

- **Contract слой** - интерфейсы (`IRabbitMqPublisher`)
- **DAL слой** - низкоуровневая работа с RabbitMQ (`RabbitMqPublisher`)
- **BLL слой** - высокоуровневые сервисы (`RabbitMqPublishingService`, `RabbitMqListener`)

## Конфигурация

Все данные для подключения к RabbitMQ берутся из конфигурации приложения.

### appsettings.json

```json
{
  "RabbitMqCreds": {
    "Host": "localhost",
    "Port": 5672,
    "Username": "guest",
    "Password": "guest",
    "VirtualHost": "/"
  }
}
```

### appsettings.Development.json

```json
{
  "RabbitMqCreds": {
    "Host": "rabbitmq-dev-server",
    "Port": 5672,
    "Username": "dev_user",
    "Password": "dev_password",
    "VirtualHost": "/dev"
  }
}
```

## Использование

### 1. Отправка сообщений через RabbitMqPublishingService (BLL слой)

Это основной способ работы с RabbitMQ для отправки сообщений. Используйте зависимость `RabbitMqPublishingService`:

```csharp
public class MyController : ControllerBase
{
    private readonly RabbitMqPublishingService _publishingService;

    public MyController(RabbitMqPublishingService publishingService)
    {
        _publishingService = publishingService;
    }

    [HttpPost("send-message")]
    public async Task<IActionResult> SendMessage(string message)
    {
        try
        {
            // Отправка текстового сообщения
            await _publishingService.SendMessageAsync("my-queue", message);
            
            return Ok("Сообщение отправлено");
        }
        catch (Exception ex)
        {
            return BadRequest($"Ошибка: {ex.Message}");
        }
    }

    [HttpPost("send-json")]
    public async Task<IActionResult> SendJsonMessage(object data)
    {
        try
        {
            var json = JsonSerializer.Serialize(data);
            // Отправка JSON сообщения
            await _publishingService.SendJsonMessageAsync("my-queue", json);
            
            return Ok("JSON сообщение отправлено");
        }
        catch (Exception ex)
        {
            return BadRequest($"Ошибка: {ex.Message}");
        }
    }
}
```

### 2. Прямое использование IRabbitMqPublisher (DAL слой)

Для более низкоуровневого доступа можно использовать интерфейс `IRabbitMqPublisher`:

```csharp
public class MyService
{
    private readonly IRabbitMqPublisher _publisher;

    public MyService(IRabbitMqPublisher publisher)
    {
        _publisher = publisher;
    }

    public async Task PublishMessage()
    {
        await _publisher.PublishAsync(
            queueName: "my-queue",
            contentType: "application/json",
            message: "{\"key\": \"value\"}");
    }
}
```

### 3. Включение фонового слушателя (BackgroundService)

Если требуется слушать сообщения из RabbitMQ, раскомментируйте строку в `Program.cs`:

```csharp
// RabbitMQ сервисы
builder.Services.AddSingleton<RabbitMqMessageStore>();
builder.Services.AddSingleton<IRabbitMqPublisher, RabbitMqPublisher>();
builder.Services.AddSingleton<RabbitMqPublishingService>();
// Раскомментируйте для включения фонового слушателя сообщений из RabbitMQ
builder.Services.AddHostedService<RabbitMqListener>();
```

Затем получить последнее сообщение из хранилища:

```csharp
public class MyController : ControllerBase
{
    private readonly RabbitMqMessageStore _messageStore;

    public MyController(RabbitMqMessageStore messageStore)
    {
        _messageStore = messageStore;
    }

    [HttpGet("last-message")]
    public IActionResult GetLastMessage()
    {
        var message = _messageStore.Get();
        if (message == null)
            return NotFound("Нет полученных сообщений");

        return Ok(new { message });
    }
}
```

## Структура классов

### Contract слой (`NsiTransfer.Contract.Interfaces`)

- `IRabbitMqPublisher` - интерфейс для публикации сообщений

### DAL слой (`NsiTransfer.DAL.Network`)

- `RabbitMqPublisher` - реализация интерфейса `IRabbitMqPublisher`, работает с подключением и каналом RabbitMQ

### BLL слой (`NsiTransfer.BLL.Services`)

- `RabbitMqPublishingService` - удобный сервис для отправки сообщений с логированием
- `RabbitMqMessageStore` - хранилище последних полученных сообщений
- `RabbitMqListener` (`NsiTransfer.BLL.BackgroundServices`) - фоновый сервис для прослушивания сообщений

## Особенности

1. **Автоматическое восстановление соединения** - при разрыве соединения `RabbitMqPublisher` автоматически переподключится
2. **Логирование** - все операции логируются через `ILogger`
3. **Обработка ошибок** - исключения обрабатываются и логируются на всех уровнях
4. **Thread-safe** - все операции с соединением защищены блокировкой
5. **Правильное освобождение ресурсов** - используется `IDisposable` для корректного закрытия соединения

## Регистрация сервисов

В `Program.cs` проводится следующая регистрация:

```csharp
builder.Services.AddSingleton<RabbitMqMessageStore>();
builder.Services.AddSingleton<IRabbitMqPublisher, RabbitMqPublisher>();
builder.Services.AddSingleton<RabbitMqPublishingService>();
// builder.Services.AddHostedService<RabbitMqListener>(); // Раскомментируйте при необходимости
```

## Миграция со старого кода

Старый класс `RabbitMqUtils` отмечен как `[Obsolete]` и больше не используется.

**Было:**
```csharp
var utils = new RabbitMqUtils(options);
await utils.SimplePublish("queue-name", "application/json", message);
```

**Теперь:**
```csharp
var publishingService = new RabbitMqPublishingService(publisher, logger);
await publishingService.SendJsonMessageAsync("queue-name", message);
```

Или еще проще через внедрение зависимости в конструктор контроллера/сервиса.
