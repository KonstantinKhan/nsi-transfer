# Архитектура приложения

Общая архитектура системы NsiTransfer и взаимодействие компонентов.

## Слои приложения

```
┌─────────────────────────────────────────┐
│   Фронтенд (Vue.js / TypeScript)        │
└────────────────┬────────────────────────┘
                 │
        HTTP API (Swagger)
                 │
┌────────────────┴────────────────────────┐
│   API Controller (REST endpoints)       │  ← PolynomSyncController
├─────────────────────────────────────────┤
│   Business Logic Layer (BLL)            │  ← ISyncUseCases, IPolynomApiService
├─────────────────────────────────────────┤
│   Data Access Layer (DAL)               │  ← Repository pattern
├─────────────────────────────────────────┤
│   Database (PostgreSQL)                 │
│   Message Broker (RabbitMQ)             │  ← Async events
│   External API (Полином)                │  ← HTTP client
└─────────────────────────────────────────┘
```

## Основные компоненты

### 1. Controllers (Presentation Layer)

**Путь:** `NsiTransfer/Presentation/Controllers/`

- `PolynomSyncController` — управление синхронизацией данных
- `AdminController` — администрирование (выбор справочника)

**Роль:** обработка HTTP запросов от фронтенда

### 2. Business Logic Layer (BLL)

**Путь:** `NsiTransfer.BLL/`

**Ключевые классы:**

| Класс | Интерфейс | Назначение |
|-------|-----------|-----------|
| `PolynomApiService` | `IPolynomApiService` | Бизнес-логика работы с API |
| `ClassificationCodeProcessor` | `IClassificationCodeProcessor` | Обработчик кодов классификатора |
| `SyncUseCases` | `ISyncUseCases` | Use-cases синхронизации |

### 3. Data Access Layer (DAL)

**Путь:** `NsiTransfer.DAL/`

**Компоненты:**

- **Repositories** — доступ к данным
  - `PolynomApiHttpRepository` — HTTP клиент для API
  - `MessageRepository` — работа с RabbitMQ
  - `GenericRepository<T>` — CRUD операции

- **Database** — работа с PostgreSQL
  - `AppDbContext` — Entity Framework контекст
  - Migrations — миграции БД

### 4. Contracts (Models & DTOs)

**Путь:** `NsiTransfer.Contract/`

**Основные модели:**

- `PolynomObjectWithShortProperties` — объект Полинома с кратким описанием
- `ClassificationTreeNode` — узел дерева классификации
- `PropertyOwnerResponseCustom` — свойства объекта
- `Message` — внутреннее сообщение синхронизации

## Процесс синхронизации

```
POST /api/polynom-sync/start-data-collection-in-background
    │
    ├─→ PolynomSyncController.StartDataCollectionInBackgroundAsync()
    │
    ├─→ ISyncUseCases.StartDataCollectionInBackgroundAsync()
    │
    ├─→ IPolynomApiService.GetDiffsInTimePeriod()
    │       │
    │       └─→ PolynomApiHttpRepository.ExecuteSearchProperty()
    │           └─→ POST /api/v1/search/execute-property-search
    │
    └─→ For each object:
        ├─→ IClassificationCodeProcessor.ProcessAsync()
        │   ├─→ IPolynomApiService.GetParentGroupsWithProperties()
        │   ├─→ IPolynomApiService.GetLastClassificationCodeInGroup()
        │   └─→ IPolynomApiService.UpdateClassificationCodeAsync()
        │       └─→ PolynomApiHttpRepository.SetPropertyValuesOfPropertyOwner()
        │           └─→ POST /api/v1/property-owner/set-property-values
        │
        └─→ Publish to RabbitMQ
            └─→ nsitransfer.exchange → polynom.search.results
```

## Внешние системы

### Полином API

**Тип:** REST API (JSON over HTTP)

**Эндпоинты:** см. [[polynom-api]]

**Аутентификация:** Bearer токен (получается через `/api/v1/login`)

**Таймауты:**
- Handshake: 10 сек
- RPC операции: 10 сек
- Heartbeat: 60 сек

### PostgreSQL

**Назначение:** хранилище синхронизированных данных

**Таблицы:**
- `Messages` — входящие сообщения синхронизации
- `PolynomObjects` — логи объектов
- `PolynomObjectFailures` — ошибки обработки

### RabbitMQ

**Назначение:** асинхронная публикация событий

**Очереди:**
- `nsitransfer.exchange` — обменник
- `polynom.search.results` — очередь результатов

**Параметры переподключения:** 3 попытки с интервалом 10 сек

## Обработка ошибок

**Стратегия:** `Result<T>` паттерн

Все операции возвращают `Result<T>`:

```csharp
public class Result<T>
{
    public bool IsSuccess { get; set; }
    public T Data { get; set; }
    public string ErrorMessage { get; set; }
}
```

При ошибке:
1. Логируется в файл / консоль
2. Сохраняется `PolynomObjectFailure` в БД
3. Отправляется событие в RabbitMQ

## Конфигурация загрузки

**Класс:** `WebApplicationBuilderExtensions.BindConfigModels()`

Загружает конфигурацию в порядке:

1. `appsettings.json`
2. `appsettings.Development.json`
3. Переменные окружения
4. `configuration.json` (AppConfiguration)

## Связанные страницы

- [[project-structure]] — Структура папок проекта
- [[configuration]] — Конфигурация приложения
- [[polynom-api]] — API интеграция
- [[classification]] — Система классификации

