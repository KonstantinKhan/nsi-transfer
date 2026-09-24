# Переменные окружения

Полный список переменных окружения, используемых приложением NsiTransfer.

## Формат переменных

В ASP.NET Core используется формат: `Section__Property`

Двойное подчёркивание (`__`) разделяет уровни иерархии конфигурации.

**Примеры:**
- `RabbitMqCreds__Host` → `RabbitMqCreds.Host`
- `PolynomConfig__Address` → `PolynomConfig.Address`
- `SmtpSettings__Server` → `SmtpSettings.Server`

## RabbitMQ

| Переменная | Значение | Описание |
|------------|----------|---------|
| `RabbitMqCreds__Host` | `localhost` | Хост брокера сообщений |
| `RabbitMqCreds__Port` | `5672` | Порт AMQP |
| `RabbitMqCreds__Username` | `guest` | Пользователь |
| `RabbitMqCreds__Password` | `guest` | Пароль |
| `RabbitMqCreds__VirtualHost` | `/` | Виртуальный хост |

**Развёртывание:**
- Локально: `localhost`
- Docker: `host.docker.internal`

## Полином API

| Переменная | Значение | Описание |
|------------|----------|---------|
| `PolynomConfig__Address` | `http://127.0.0.1:5100/` | URL API сервера Полинома |
| `PolynomConfig__DbName` | `polynom` | Имя базы данных |
| `PolynomConfig__TimeZoneId` | `Russian Standard Time` | Часовой пояс |
| `PolynomAuthCreds__Username` | `admin` | Логин для API |
| `PolynomAuthCreds__Password` | `123` | Пароль для API |

## PostgreSQL

| Переменная | Значение | Описание |
|------------|----------|---------|
| `ConnectionStrings__DefaultConnection` | `Host=localhost;Port=5432;...` | Строка подключения к БД |

**Формат:**
```
Host=localhost;Port=5432;Database=nsi_transfer;Username=postgres;Password=rJpthUvW366;Include Error Detail=true
```

## SMTP (Email)

| Переменная | Значение | Описание |
|------------|----------|---------|
| `SmtpSettings__Server` | `smtp.ascon.ru` | SMTP сервер |
| `SmtpSettings__Port` | `465` | Порт (обычно 587 или 465) |
| `SmtpSettings__SenderName` | `NsiTransfer` | Отправитель |
| `SmtpSettings__SenderEmail` | `han@ascon.ru` | Email отправителя |
| `SmtpSettings__Username` | `user@ascon.ru` | Логин SMTP |
| `SmtpSettings__Password` | `password` | Пароль SMTP |
| `SmtpSettings__UseSsl` | `true` | Использовать SSL/TLS |

## Приложение

| Переменная | Значение | Описание |
|------------|----------|---------|
| `ASPNETCORE_ENVIRONMENT` | `Development` / `Production` | Режим приложения |
| `BACKEND_PORT` | `8080` | Порт бэка (только Docker) |
| `FRONTEND_PORT` | `5173` | Порт фронта (только Docker) |

## Фронтенд

| Переменная | Значение | Описание |
|------------|----------|---------|
| `VITE_API_BASE_URL` | `http://localhost:8080/api` | URL API для браузера |

**Примеры:**
- Локально на одной машине: `http://localhost:8080/api`
- Удалённо: `http://192.168.0.50:8080/api`

**В Docker:** читается в рантайме, не запекается в бандл при сборке. `docker-entrypoint.sh`
генерирует `/config.json` из `VITE_API_BASE_URL` при старте контейнера, фронт фетчит его
перед загрузкой приложения (`index.html` → `window.__APP_CONFIG__` → `apiConfig.ts`).
Смена значения требует только пересоздания контейнера, без пересборки образа:
```bash
docker compose up -d --force-recreate nsitransferfront
```

## Логирование

Механизм — Serilog, настраивается в `WebApplicationBuilderExtensions.ConfigureLogging()`.
Два санка:

- **Консоль** — всегда (видно через `docker compose logs nsitransfer`)
- **Файл** — только при `UseFileLogging=true` (в `appsettings.json` по умолчанию `false`,
  в `.env` по умолчанию не задан → в Docker по умолчанию пишется только в консоль)

Переменные:

| Переменная | Значение | Описание |
|------------|----------|---------|
| `LoggingConfig__UseFileLogging` | `false` | Логирование в файлы |
| `LoggingConfig__LOGS_MAX_FOLDER_SIZE_BYTES` | `104857600` | Макс размер папки логов (100 МБ); 0 = автоочистка выключена |
| `LoggingConfig__LOGS_CLEANUP_INTERVAL_SECONDS` | `300` | Интервал очистки логов |

Файловый режим (в Docker):

- путь: `/app/Logs/log-YYYYMMDD.txt` — rolling по дням (`RollingInterval.Day`); маунт `./backend/logs:/app/Logs`
  → файлы на хосте в `backend/logs/`
- хранение: 62 файла (`retainedFileCountLimit`); автоочистка папки по лимиту 100 МБ, проверка раз в 300 сек
- уровни: `Debug`+ для приложения; `Microsoft*` — Warning; EF Core SQL — только Error
- каталог `backend/logs` должен принадлежать uid 1654 (см. [[deployment]], п.4.1)

Включение — добавить в `.env` (в любое место, порядок не важен):

```bash
LoggingConfig__UseFileLogging=true
```

затем `docker compose up -d` — **`restart` не перечитывает `.env`**.

## Приоритет конфигурации

1. **launchSettings.json** (локальная отладка из VS Code)
2. **.env** файл (Docker контейнеры)
3. **appsettings.json** (базовые настройки)
4. **appsettings.Development.json** (разработка)
5. **Переменные окружения системы** (перекрывают JSON)

## Загрузка конфигурации

**Класс:** `WebApplicationBuilderExtensions.BindConfigModels()`

```csharp
// Загружаем configuration.json из пути (по умолчанию AppContext.BaseDirectory)
var configurationPath = Environment.GetEnvironmentVariable("CONFIG_FILE_PATH")
    ?? Path.Combine(AppContext.BaseDirectory, "configuration.json");

builder.Configuration.AddJsonFile(configurationPath, optional: false, reloadOnChange: true);
```

## Пример для локальной разработки

**launchSettings.json:**

```json
{
  "profiles": {
    "http": {
      "environmentVariables": {
        "ASPNETCORE_ENVIRONMENT": "Development",
        "RabbitMqCreds__Host": "localhost",
        "RabbitMqCreds__Port": "5672",
        "PolynomConfig__Address": "http://127.0.0.1:5100/",
        "PolynomConfig__DbName": "polynom",
        "VITE_API_BASE_URL": "http://localhost:8080/api"
      }
    }
  }
}
```

## Связанные страницы

- [[configuration]] — Детальное описание конфигурации
- [[rabbitmq]] — RabbitMQ переменные
- [[polynom-api]] — Полином API переменные

