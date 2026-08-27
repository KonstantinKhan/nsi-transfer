# RabbitMQ брокер сообщений

RabbitMQ используется для асинхронной публикации событий синхронизации данных.

## Запуск RabbitMQ

### Вариант 1: Docker (рекомендуется)

```bash
docker run -d --name rabbitmq -p 5672:5672 -p 15672:15672 rabbitmq:4-management
```

**Параметры:**
- `-d` — запустить в фоне
- `-p 5672:5672` — порт AMQP (связь с приложением)
- `-p 15672:15672` — Management UI (веб-интерфейс администратора)
- `rabbitmq:4-management` — образ с Management плагином

**Management UI:** `http://localhost:15672/`
- Логин: `guest`
- Пароль: `guest`

### Вариант 2: Локально на Windows

Установи через Chocolatey:
```powershell
choco install rabbitmq
```

## Проверка подключения

```bash
# Linux/Mac
telnet localhost 5672

# PowerShell
Test-NetConnection -ComputerName localhost -Port 5672
```

Должно вернуть успешное соединение (порт слушает).

## Конфигурация в приложении

**Переменные окружения (launchSettings.json):**

```json
"RabbitMqCreds__Host": "localhost",
"RabbitMqCreds__Port": "5672",
"RabbitMqCreds__Username": "guest",
"RabbitMqCreds__Password": "guest",
"RabbitMqCreds__VirtualHost": "/"
```

**В Docker (.env):**

```bash
RabbitMqCreds__Host=host.docker.internal
RabbitMqCreds__Port=5672
RabbitMqCreds__Username=guest
RabbitMqCreds__Password=guest
RabbitMqCreds__VirtualHost=/
```

## Точки подключения

Приложение подключается к RabbitMQ через:

**Класс:** `ConnectionFactoryCreator` (`NsiTransfer.DAL/Services/ConnectionFactoryCreator.cs`)

```csharp
var factory = new RabbitMQ.Client.ConnectionFactory
{
    HostName = creds.Host,           // localhost
    Port = creds.Port,               // 5672
    UserName = creds.Username,       // guest
    Password = creds.Password,       // guest
    VirtualHost = creds.VirtualHost, // /
    RequestedHeartbeat = TimeSpan.FromSeconds(60),
    ContinuationTimeout = TimeSpan.FromSeconds(10),
    HandshakeContinuationTimeout = TimeSpan.FromSeconds(10)
};
```

## Очереди и обмены

**Конфигурация (configuration.json):**

```json
"RabbitMqQueues": {
  "NsiTransferExchangeName": "nsitransfer.exchange",
  "PolynomSearchResultsQueueName": "polynom.search.results"
}
```

**Параметры переподключения:**

```json
"RabbitMqRetryParams": {
  "MaxRetryAttempts": 3,
  "RetryIntervalInSeconds": 10,
  "ConfirmationTimeOutInSeconds": 30
}
```

## Ошибки и решения

### BrokerUnreachableException

**Ошибка:** `None of the specified endpoints were reachable`

**Причины:**
1. RabbitMQ не запущен
2. Порт 5672 заблокирован firewall
3. Неправильный адрес в конфигурации (используется `host.docker.internal` вместо `localhost`)

**Решение:** см. [[rabbitmq#Запуск RabbitMQ|Запуск RabbitMQ]] выше.

## Связанные страницы

- [[configuration]] — Конфигурация приложения
- [[sync-flow]] — Процесс синхронизации (использует RabbitMQ)
- [[environment]] — Переменные окружения

