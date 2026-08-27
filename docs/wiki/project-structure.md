# Структура проекта

Организация файлов и папок в проекте NsiTransfer.

## Общая структура

```
nsitransfer/
├── backend/                          # Бэкенд (ASP.NET Core)
│   └── nsitransfer/
│       └── NsiTransfer/              # Главный проект
├── frontend/                         # Фронтенд (Vue.js)
│   └── nsitransferfront/
├── docs/                             # Документация
│   └── wiki/                         # Wiki (эта папка)
├── test-infra/                       # Инфраструктура для тестов
├── docker-compose.yml                # Docker конфигурация
├── DEPLOY.md                         # Инструкции развёртывания
├── HANDOFF.md                        # Передача проекта
└── .env                              # Переменные окружения

```

## Backend структура

### NsiTransfer.csproj (главный проект)

```
backend/nsitransfer/NsiTransfer/
├── Properties/
│   └── launchSettings.json           # Конфигурация запуска (F5)
│
├── Presentation/
│   └── Controllers/
│       ├── PolynomSyncController.cs  # REST API синхронизации
│       └── AdminController.cs        # Администрирование
│
├── Extensions/
│   └── WebApplicationBuilderExtensions.cs  # Конфигурация приложения
│
├── Program.cs                        # Точка входа
│
└── Dockerfile                        # Docker образ
```

### NsiTransfer.BLL (Business Logic Layer)

```
NsiTransfer.BLL/
├── Interfaces/
│   ├── Services/
│   │   ├── IPolynomApiService.cs     # Интерфейс API сервиса
│   │   └── IClassificationCodeProcessor.cs  # Интерфейс процессора кодов
│   └── UseCases/
│       └── ISyncUseCases.cs          # Интерфейс use cases
│
├── Services/
│   ├── PolynomApiService.cs          # Бизнес-логика API
│   ├── ClassificationCodeProcessor.cs  # Обработчик кодов
│   └── SyncUseCases.cs               # Use cases синхронизации
│
├── Tools/
│   └── PolynomRequestBuilder.cs      # Построитель запросов к API
│
└── HostExtensions.cs                 # Регистрация сервисов DI
```

### NsiTransfer.DAL (Data Access Layer)

```
NsiTransfer.DAL/
├── Repositories/
│   ├── Network/
│   │   ├── BaseHttpRepository.cs     # Базовый класс HTTP репозиториев
│   │   ├── PolynomApiHttpRepository.cs  # HTTP клиент Полинома
│   │   └── PolynomAuthHttpRepository.cs # Аутентификация
│   │
│   └── Db/
│       ├── GenericRepository.cs      # Универсальный репозиторий
│       ├── MessageRepository.cs      # Работа с сообщениями
│       └── UnitOfWork.cs             # Unit of Work паттерн
│
├── Db/
│   ├── Context/
│   │   └── AppDbContext.cs           # Entity Framework контекст
│   │
│   ├── Entities/
│   │   ├── PolynomObject.cs          # Лог обработанного объекта
│   │   ├── PolynomObjectFailure.cs   # Лог ошибки
│   │   └── Message.cs                # Сообщение синхронизации
│   │
│   └── Migrations/
│       └── [миграции БД]
│
├── Routes/
│   └── ApiRoutes.cs                  # Определение эндпоинтов API
│
└── Interfaces/
    ├── Http/
    │   └── IPolynomApiHttpRepository.cs
    └── Db/
        └── IUnitOfWork.cs
```

### NsiTransfer.Contract (Models & Configuration)

```
NsiTransfer.Contract/
├── ConfigModels/
│   ├── RabbitMqCreds.cs              # Конфигурация RabbitMQ
│   ├── PolynomConfig.cs              # Конфигурация API Полинома
│   ├── SmtpSettings.cs               # Конфигурация SMTP
│   └── AppConfiguration.cs           # Главная конфигурация
│
├── Models/
│   ├── Ascon/
│   │   └── PolynomObjectWithShortProperties.cs  # Объект Полинома
│   │
│   ├── DTO/
│   │   └── [Data Transfer Objects]
│   │
│   └── Common/
│       └── Result<T>.cs              # Результат операции
│
└── Enums/
    └── PolynomObjectFailureTypeEnum.cs  # Типы ошибок
```

### NsiTransfer.Tests

```
NsiTransfer.Tests/
├── BllTests/
│   └── SyncUseCasesTests.cs
├── DalTests/
│   └── RabbitMqPublisherTests.cs
└── [другие тесты]
```

## Конфигурационные файлы

### launchSettings.json

**Путь:** `backend/nsitransfer/NsiTransfer/Properties/launchSettings.json`

Конфигурация для запуска из VS Code (F5).

**Используется:** локальная разработка

```json
{
  "profiles": {
    "http": {
      "commandName": "Project",
      "environmentVariables": {
        "ASPNETCORE_ENVIRONMENT": "Development",
        "RabbitMqCreds__Host": "localhost",
        "PolynomConfig__Address": "http://127.0.0.1:5100/"
      }
    }
  }
}
```

### .env

**Путь:** `/` (корень проекта)

Переменные окружения для Docker контейнеров.

**Используется:** развёртывание в Docker

```bash
ASPNETCORE_ENVIRONMENT=Production
RabbitMqCreds__Host=host.docker.internal
PolynomConfig__Address=http://host.docker.internal:5100/
```

### configuration.json

**Пути:**
- Исходный: `backend/nsitransfer/config/configuration.json`
- Скомпилированный: `backend/nsitransfer/NsiTransfer/bin/Debug/net8.0/configuration.json`

Конфигурация приложения (AppConfiguration).

```json
{
  "AppConfiguration": {
    "TargetReferenceNode": { /* ... */ },
    "RabbitMqQueues": { /* ... */ },
    "RabbitMqRetryParams": { /* ... */ },
    "PolynomApiSyncOptions": { /* ... */ }
  }
}
```

## Слой контроллеров

### PolynomSyncController

**Пути:** `Presentation/Controllers/PolynomSyncController.cs`

**Методы:**

| Метод | Маршрут | Назначение |
|-------|---------|-----------|
| `StartDataCollectionAndWaitAsync` | `POST /api/polynom-sync/start-data-collection-and-wait` | Синхр. с ожиданием (аутентифицированный) |
| `StartDataCollectionInBackgroundAsync` | `POST /api/polynom-sync/start-data-collection-in-background` | Фоновая синхр. |
| `ListenForSyncEvents` | `GET /api/polynom-sync/listen-for-sync-events?sendingId={id}` | SSE события синхр. |
| `GetSendings` | `GET /api/polynom-sync/sendings` | Получить список синхр. |
| `GetSending` | `GET /api/polynom-sync/sending?sendingId={id}` | Получить синхр. по ID |

## База данных

### Таблицы

| Таблица | Назначение | Путь сущности |
|---------|-----------|---------------|
| `Messages` | Сообщения синхронизации | `DAL/Db/Entities/Message.cs` |
| `PolynomObjects` | Логи успешно обработанных объектов | `DAL/Db/Entities/PolynomObject.cs` |
| `PolynomObjectFailures` | Логи ошибок обработки | `DAL/Db/Entities/PolynomObjectFailure.cs` |

### Миграции

**Путь:** `backend/nsitransfer/NsiTransfer/DAL/Db/Migrations/`

Для применения миграций:

```bash
cd backend/nsitransfer/NsiTransfer
dotnet ef database update
```

## Docker

### Dockerfile

**Путь:** `backend/nsitransfer/NsiTransfer/Dockerfile`

Образ контейнера бэка (ASP.NET Core).

### docker-compose.yml

**Путь:** `/` (корень проекта)

Сервисы для разработки и тестирования:
- `nsitransfer` — бэк
- `nsitransferfront` — фронт

```yaml
services:
  nsitransfer:
    build: ./backend/nsitransfer
    ports:
      - "8080:8080"
    environment:
      - ASPNETCORE_ENVIRONMENT=Production
```

## Frontend структура

```
frontend/nsitransferfront/
├── src/
│   ├── components/         # Vue компоненты
│   ├── views/             # Страницы
│   ├── services/          # HTTP клиент
│   └── App.vue            # Главный компонент
├── package.json           # npm зависимости
├── vite.config.js        # Vite конфигурация
└── Dockerfile            # Docker образ
```

## Логирование

**Конфигурация:**

- Serilog — структурированное логирование
- Вывод в консоль + файлы (в папке `Logs/`)
- Уровень: `Information` (разработка), `Warning` (production)

**Файлы:**

```
Logs/
└── log-20260825.txt      # Логи по датам
```

## Связанные страницы

- [[architecture]] — Архитектура приложения
- [[configuration]] — Конфигурация
- [[environment]] — Переменные окружения

