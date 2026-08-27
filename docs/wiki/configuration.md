# Конфигурация приложения

Применяется три уровня конфигурации с приоритетом.

## Иерархия конфигурации

1. **launchSettings.json** (локальная отладка) → переменные окружения профиля
2. **.env** (Docker контейнер) → переменные окружения системы
3. **configuration.json** (AppConfiguration) → JSON-конфиг приложения

## launchSettings.json

**Путь:** `backend/nsitransfer/NsiTransfer/Properties/launchSettings.json`

Используется при запуске из VS Code (F5). Определяет переменные окружения для профиля `http`.

```json
{
  "profiles": {
    "http": {
      "commandName": "Project",
      "environmentVariables": {
        "ASPNETCORE_ENVIRONMENT": "Development",
        "RabbitMqCreds__Host": "localhost",
        "RabbitMqCreds__Port": "5672",
        "PolynomConfig__Address": "http://127.0.0.1:5100/",
        "PolynomConfig__DbName": "polynom"
      }
    }
  }
}
```

**Ключевые переменные:**
- `RabbitMqCreds__*` — настройки подключения к RabbitMQ
- `PolynomConfig__*` — настройки подключения к API Полинома
- `SmtpSettings__*` — параметры SMTP для уведомлений об ошибках

## .env

**Путь:** `/` (корень проекта)

Используется в Docker контейнерах. Копируется в переменные окружения при запуске сервиса.

```bash
ASPNETCORE_ENVIRONMENT=Production
BACKEND_PORT=8080
FRONTEND_PORT=5173

# PostgreSQL
ConnectionStrings__DefaultConnection=Host=host.docker.internal;Port=5432;...

# Полином
PolynomConfig__Address=http://host.docker.internal:5100/
PolynomConfig__DbName=polynom
PolynomAuthCreds__Username=admin
PolynomAuthCreds__Password=123

# RabbitMQ
RabbitMqCreds__Host=host.docker.internal
RabbitMqCreds__Port=5672
RabbitMqCreds__Username=guest
RabbitMqCreds__Password=guest
```

**Важно:** для Docker используется `host.docker.internal` вместо `localhost`.

Для локальной разработки из VS Code используй `localhost`.

## configuration.json

**Пути:**
- `backend/nsitransfer/config/configuration.json` (исходный)
- `backend/nsitransfer/NsiTransfer/bin/Debug/net8.0/configuration.json` (скомпилированный)

Содержит AppConfiguration приложения (не переменные окружения).

```json
{
  "AppConfiguration": {
    "TargetReferenceNode": {
      "TargetReferenceNodeObjectId": 61,
      "TargetReferenceNodeTypeId": 48,
      "TargetReferenceNodeName": "Коды"
    },
    "RabbitMqQueues": {
      "NsiTransferExchangeName": "nsitransfer.exchange",
      "PolynomSearchResultsQueueName": "polynom.search.results"
    },
    "RabbitMqRetryParams": {
      "MaxRetryAttempts": 3,
      "RetryIntervalInSeconds": 10
    },
    "PolynomApiSyncOptions": {
      "StartSyncWithIntervalMinutes": 15,
      "ConceptNameForClassificationData": "Данные классификатора",
      "ClassificationCodePropertyName": "Код классификатора",
      "OwnContractName": "Код по справочнику",
      "MinCodePropertyName": "МинКод",
      "MaxCodePropertyName": "МаксКод"
    }
  }
}
```

## Порядок загрузки

При запуске приложения конфигурация загружается в порядке:

1. `appsettings.json` (базовые настройки)
2. `appsettings.Development.json` (разработка)
3. Переменные окружения (перекрывают JSON)
4. `configuration.json` (AppConfiguration через ConfigurationBuilder)

## PolynomApiSyncOptions (полный DTO)

**Класс:** `PolynomApiSyncOptions` (`NsiTransfer.Contract/ConfigModels/AppConfiguration.cs`)

Содержит как параметры синхронизации, так и параметры обработки классификационных кодов.

| Поле | Тип | Назначение | Пример |
|------|-----|-----------|--------|
| `StartSyncWithIntervalMinutes` | `int` | Интервал автоматической синхронизации (минуты). Используется BackgroundService для периодического запуска синхронизации. | `15` |
| `ConceptNameForClassificationData` | `string` | Название концепции в Полиноме, содержащей данные классификатора. Используется при поиске свойств объекта. | `"Данные классификатора"` |
| `ClassificationCodePropertyName` | `string` | Название свойства объекта, в котором хранится код классификатора. Используется при чтении/записи кода. | `"Код классификатора"` |
| `OwnContractName` | `string` | Название справочника (контракта) для классификационных кодов. Используется при определении группы для кода. | `"Код по справочнику"` |
| `MinCodePropertyName` | `string` | Название свойства, хранящего минимальное значение кода в группе. Используется при валидации диапазона кода. | `"МинКод"` |
| `MaxCodePropertyName` | `string` | Название свойства, хранящего максимальное значение кода в группе. Используется при валидации диапазона кода. | `"МаксКод"` |

**Важно:** Все 6 полей обязательны (`[Required]`) для целостности конфигурации и должны быть заполнены при сохранении через API. Они используются на разных этапах синхронизации и обработки классификации.

### Сохранение через UI (PUT /api/configuration/polynom-api-sync-options)

Endpoint заменяет всю секцию `PolynomApiSyncOptions` целиком, а не отдельные поля (whole-section replace). Frontend должен отправить все 6 полей, иначе невопроизведённые поля будут `null` и фейльнут валидацию `[Required]`. 

**Пример корректного payload:**
```json
{
  "startSyncWithIntervalMinutes": 20,
  "conceptNameForClassificationData": "Данные классификатора",
  "classificationCodePropertyName": "Код классификатора",
  "ownContractName": "Код по справочнику",
  "minCodePropertyName": "МинКод",
  "maxCodePropertyName": "МаксКод"
}
```

## Известные issues и fixes

### Ошибка при сохранении интервала синхронизации (HTTP 400)

**Проблема (исправлено):** 

При попытке изменить интервал синхронизации в UI (`main/config/polynom`) и сохранить возникала ошибка:
```
HTTP 400 Bad Request: One or more validation errors occurred
```

**Причина:**

- Backend endpoint `PUT /api/configuration/polynom-api-sync-options` работает через whole-section replace (заменяет всю секцию целиком)
- Frontend отправлял только `{ startSyncWithIntervalMinutes: N }`, теряя остальные 5 обязательных полей
- При десериализации они биндились в `null`, что фейльило валидацию `[Required]`

**Фикс (август 2026):**

Frontend теперь:
1. Загружает полный объект `PolynomApiSyncOptions` при открытии страницы
2. Отправляет весь объект целиком (спред `...syncOptions.value`), сохраняя все 6 полей
3. TS-тип `PolynomApiSyncOptions` дополнен всеми 6 полями (раньше содержал только 1)

**Файлы изменены:**
- `frontend/nsitransferfront/src/services/configurationService.ts` — расширен интерфейс
- `frontend/nsitransferfront/src/views/config/PolynomView.vue` — фикс в `saveSyncOptions()`

**Инсайт для разработчиков:**

Если будущих endpoint будет требовать partial patch вместо whole-section replace, рассмотреть:
- Использовать JSON Patch (RFC 6902) на backend или
- Реализовать отдельный endpoint для обновления конкретных полей или
- Явно документировать в API, что требуется полный набор полей

## Связанные страницы

- [[environment]] — Детальное описание переменных окружения
- [[rabbitmq]] — RabbitMQ конфигурация
- [[polynom-api]] — API Полинома конфигурация
- [[architecture]] — Архитектура загрузки конфигурации

