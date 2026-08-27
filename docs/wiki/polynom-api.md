# Полином API

Интеграция с внешним API системы Полином для получения и синхронизации данных.

## Конфигурация подключения

**launchSettings.json:**

```json
"PolynomConfig__Address": "http://127.0.0.1:5100/",
"PolynomConfig__DbName": "polynom",
"PolynomConfig__TimeZoneId": "Russian Standard Time"
```

**Класс конфигурации:** `PolynomConfig` (`NsiTransfer.Contract/ConfigModels/PolynomConfig.cs`)

```csharp
public class PolynomConfig
{
    public string Address { get; set; }      // URL API: http://127.0.0.1:5100/
    public string DbName { get; set; }       // Имя БД: polynom
    public string TimeZoneId { get; set; }   // Часовой пояс: Russian Standard Time
}
```

## Основные эндпоинты

### Поиск

| Метод | Эндпоинт | Назначение |
|-------|----------|-----------|
| POST | `/api/v1/search/execute-property-search` | Поиск объектов по свойствам с фильтрацией |

### Классификация

| Метод | Эндпоинт | Назначение |
|-------|----------|-----------|
| POST | `/api/v1/tree/get-classification` | Получить корневой узел классификации (справочника) |
| POST | `/api/v1/tree/get-classification-node-children` | Получить дочерние узлы классификации |
| POST | `/api/v1/classification-object/get-parent-groups` | Получить родительские группы классификации |

### Свойства объектов

| Метод | Эндпоинт | Назначение |
|-------|----------|-----------|
| POST | `/api/v1/property-owner/get-properties` | Получить все понятия (концепции) и свойства объекта |
| POST | `/api/v1/property-owner/set-property-values` | **Установить значения свойств объекта** (включая коды классификатора) |

### Определения свойств

| Метод | Эндпоинт | Назначение |
|-------|----------|-----------|
| POST | `/api/v1/property-definition/get-by-absolute-code` | Получить определение свойства по абсолютному коду |
| POST | `/api/v1/concept-property-source/get-by-absolute-code` | Получить источник свойств концепции по коду |

## HTTP-клиент

**Класс:** `PolynomApiHttpRepository` (`NsiTransfer.DAL/Repositories/Network/PolynomApiHttpRepository.cs`)

Отправляет все POST запросы к API Полинома. Каждый запрос логируется:

```
[API] → POST /api/v1/... | MethodName
[API] REQUEST BODY: {...}
[API] RESPONSE BODY: {...}
[API] ✓ MethodName | /api/v1/... | Статус: 200 OK
```

## Бизнес-логика

**Класс:** `PolynomApiService` (`NsiTransfer.BLL/Services/PolynomApiService.cs`)

Реализует методы для работы с API:
- `GetClassificationRootNode()` — получить корневой справочник
- `GetClassificationChildrenNodes()` — получить дочерние элементы
- `GetAllPropertiesOfObject()` — получить свойства объекта
- `GetParentGroupsWithProperties()` — получить родительские группы со свойствами
- `UpdateClassificationCodeAsync()` — установить код классификатора
- `GetLastClassificationCodeInGroup()` — найти максимальный код в группе

## Обработка ошибок

Все ошибки API возвращаются в виде `Result<T>`:

```csharp
if (!result.IsSuccess)
{
    return $"Ошибка API: {result.ErrorMessage}";
}
```

**Возможные ошибки:**
- Сервер недоступен (BrokerUnreachableException)
- HTTP ошибки (4xx, 5xx)
- Ошибки десериализации JSON
- Таймауты (10 секунд для RPC операций)

## Связанные страницы

- [[classification-api]] — API методы для классификации
- [[http-logging]] — Логирование HTTP запросов
- [[sync-flow]] — Процесс синхронизации
- [[configuration]] — Конфигурация подключения

