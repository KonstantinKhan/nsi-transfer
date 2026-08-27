# HTTP логирование API запросов

Система логирования всех HTTP запросов к внешним сервисам (Полином API).

## Структура логирования

Каждый запрос к API логируется в три этапа:

### 1. Начало запроса

```
[API] → POST /api/v1/... | MethodName (описание)
```

**Пример:**
```
[API] → POST /api/v1/tree/get-classification-node-children | GetClassificationNodeChildren (дочерние узлы)
```

### 2. Тело запроса (REQUEST BODY)

```
[API] REQUEST BODY: {полный JSON запроса}
```

**Пример:**
```json
[API] REQUEST BODY: {
  "PageNumber": 1,
  "PageSize": 100,
  "ParentNodeObject": {
    "ObjectId": 61,
    "TypeId": 48
  },
  "FilterOptions": 0
}
```

### 3. Тело ответа (RESPONSE BODY)

```
[API] RESPONSE BODY: {первые 3000 символов JSON ответа}
```

**Пример:**
```json
[API] RESPONSE BODY: {
  "Items": [
    {
      "NodeObject": {"ObjectId": 123, "TypeId": 48},
      "NodeName": "Коды",
      "ItemsCount": 50
    }
  ],
  "HasNextPage": false,
  "PageNumber": 1
}
```

### 4. Результат запроса

```
[API] ✓ MethodName | /api/v1/... | Статус: 200 OK | Размер: XXXX байт
```

**При ошибке:**
```
[API] ✗ MethodName | /api/v1/... | Статус: 500 | Ошибка: {...}
```

## Методы, которые логируют запросы

### Классификация

- `GetClassificationTreeNode()` — получить корневой узел
- `GetClassificationNodeChildren()` — получить дочерние узлы
- `GetParentGroups()` — получить родительские группы

### Свойства

- `GetAllPropertiesOfPropertyOwner()` — получить все свойства объекта
- `SetPropertyValuesOfPropertyOwner()` — **установить код классификатора**

### Поиск

- `ExecuteSearchProperty()` — поиск объектов

### Определения

- `GetPropertyDefinition()` — получить определение свойства
- `GetConceptPropertySource()` — получить источник свойств концепции

## Символы статуса

| Символ | Значение | Пример |
|--------|----------|--------|
| `→` | Отправка запроса | `[API] → POST /api/v1/...` |
| `✓` | Успешный ответ (200 OK) | `[API] ✓ MethodName \| ...` |
| `✗` | Ошибка (4xx, 5xx) | `[API] ✗ MethodName \| Статус: 500` |
| `⚠` | Предупреждение (пустой ответ) | `[API] ⚠ MethodName \| Пустой ответ` |

## Пример полного лога синхронизации

```
[API] → POST /api/v1/tree/get-classification | GetClassificationTreeNode (корневой узел)
[API] REQUEST BODY: {"PageNumber": 0, "PageSize": 0, ...}
[API] RESPONSE BODY: {"Items": [...], "HasNextPage": false}
[API] ✓ GetClassificationTreeNode | /api/v1/tree/get-classification | Статус: 200 OK | Размер: 1234 байт

[API] → POST /api/v1/tree/get-classification-node-children | GetClassificationNodeChildren (дочерние узлы)
[API] REQUEST BODY: {"ParentNodeObject": {...}, "PageNumber": 1, ...}
[API] RESPONSE BODY: {"Items": [...], "HasNextPage": false}
[API] ✓ GetClassificationNodeChildren | /api/v1/tree/get-classification-node-children | Статус: 200 OK | Размер: 5678 байт

[API] → POST /api/v1/property-owner/get-properties | GetAllPropertiesOfPropertyOwner (свойства объекта)
[API] REQUEST BODY: {"Owner": {"ObjectId": 123, "TypeId": 45}}
[API] RESPONSE BODY: {"Contracts": [...], ...}
[API] ✓ GetAllPropertiesOfPropertyOwner | /api/v1/property-owner/get-properties | Статус: 200 OK | Размер: 2345 байт

[API] → POST /api/v1/property-owner/set-property-values | SetPropertyValuesOfPropertyOwner (установить код классификатора)
[API] REQUEST BODY: {"Owner": {...}, "Properties": [...]}
[API] RESPONSE BODY: {"Success": true, "Message": "Successful"}
[API] ✓ SetPropertyValuesOfPropertyOwner | /api/v1/property-owner/set-property-values | Статус: 200 OK | Размер: 56 байт
```

## Реализация

**Класс:** `PolynomApiHttpRepository` (`NsiTransfer.DAL/Repositories/Network/PolynomApiHttpRepository.cs`)

Логирование добавлено в:
1. Каждый public метод (логирует начало запроса)
2. Метод `SendAndDeserializeAsync` (логирует REQUEST BODY, RESPONSE BODY, результат)

**Logger:** `ILogger` из `BaseHttpRepository`

## Отключение DEBUG логов

Если логи слишком подробные, изменить уровень логирования в `launchSettings.json`:

```json
"Logging": {
  "LogLevel": {
    "Default": "Warning",
    "Microsoft.AspNetCore": "Warning"
  }
}
```

Уровни: `Trace`, `Debug`, `Information`, `Warning`, `Error`, `Critical`

## Связанные страницы

- [[polynom-api]] — API Полинома
- [[configuration]] — Конфигурация логирования
- [[sync-flow]] — Процесс синхронизации

