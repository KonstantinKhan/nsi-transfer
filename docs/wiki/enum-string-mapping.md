# Маппинг значений EnumString и классификационные ID

Система корректной маршрутизации значений свойств типов Enum (особенно EnumString) от Полинома в JSON сообщения RabbitMQ.

## Проблема (август 2026)

При отправке объектов в очередь RabbitMQ поле `Value` для свойств типа `PropertyType.EnumString` содержало неправильное значение:

```json
// Было (неправильно):
{
  "PropertyTypeName": "EnumString",
  "Value": "None:1565:143"  // ← Результат Object.ToString(), не реальное значение
}
```

**Причина:** `ModelMapper.SeparatePureValue()` вызывал `.ToString()` на объекте `IEnumStringItem`, который не переопределяет этот метод, поэтому возвращалась стандартная строка-идентификатор.

Аналогичная ошибка существовала для `EnumBool`, `EnumInt`, `EnumDouble`.

## Решение

### 1. Исправление извлечения значений enum типов

**Файл:** `NsiTransfer.BLL/Tools/ModelMapper.cs`, метод `SeparatePureValue()`

Изменено для всех enum типов с объектными значениями:

```csharp
// Было:
case PropertyType.EnumString when props.Values.EnumStringProperties.HasValue:
    return FindProperty(...).Value?.ToString();  // ← Ошибка

// Стало:
case PropertyType.EnumString when props.Values.EnumStringProperties.HasValue:
    return FindProperty(...).Value?.Value;  // ← Берёт реальное значение из IEnumStringItem
```

**Затронутые типы:**
- `PropertyType.EnumString` (строка)
- `PropertyType.EnumBool` (bool)
- `PropertyType.EnumInt` (int)
- `PropertyType.EnumDouble` (double)

### 2. Добавление классификационного ID для EnumString

**Файл:** `NsiTransfer.Contract/Models/DTO/PolynomShortProperty.cs`

Добавлено новое опциональное поле:

```csharp
public string? ClassifId { get; set; }
```

**Назначение:** Хранить код-описание из поля `description` выбранного enum элемента (например, "AM" для "Вспомогательные материалы").

### 3. Извлечение классификационного ID

**Файл:** `NsiTransfer.BLL/Tools/ModelMapper.cs`

Добавлен новый метод `SeparateEnumStringClassifId()`:

```csharp
private static string? SeparateEnumStringClassifId(
    IIdentifiableObject? valueIds,
    PropertyType propertyType,
    INamedObject obj,
    PropertyOwnerResponseCustom props)
{
    if (valueIds == null || propertyType != PropertyType.EnumString || !props.Values.EnumStringProperties.HasValue)
        return null;

    var enumStringProperty = FindProperty(
        props.Values.EnumStringProperties.Value,
        v => v.ObjectId == valueIds.ObjectId && v.TypeId == valueIds.TypeId,
        "Values.EnumStringProperties",
        valueIds,
        obj);

    return enumStringProperty.Value?.Description;
}
```

Вызывается из `GetValueAndDescription()` и передаётся в `PolynomShortProperty`.

### 4. Расширение сигнатуры GetValueAndDescription

**Файл:** `NsiTransfer.BLL/Tools/ModelMapper.cs:99-117`

Изменена возвращаемая кортеж с 3 на 4 элемента:

```csharp
private static (string? Value, string? Description, PropertyType? PropertyType, string? ClassifId) 
    GetValueAndDescription(...)
```

Новый 4-й элемент — `ClassifId` для EnumString.

## Структура данных из API Полинома

Из секции `definitions.enumStringProperties.items` приходят объекты вида:

```json
{
  "value": "Вспомогательные материалы",  // Берётся → Value поля в JSON
  "description": "AM",                   // Берётся → ClassifId поля в JSON
  "id": "1fe1604a-bd60-4934-9daf-98b6e902437a",
  "position": 0,
  "writeAccess": false,
  "objectId": 1565,
  "typeId": 143
}
```

## Результирующий JSON в RabbitMQ

```json
{
  "Name": "Классификатор",
  "PropertyTypeId": 10,
  "PropertyTypeName": "EnumString",
  "Value": "Вспомогательные материалы",     // ← Реальное значение
  "Description": "Описание свойства",      // ← Название свойства (определение)
  "ClassifId": "AM",                       // ← Код из description (новое поле)
  "Definition": {"ObjectId": 1565, "TypeId": 143}
}
```

## Маршрут данных

```
1. Polynom API response: definitions.enumStringProperties.items[0]
   ↓ (ObjectId/TypeId ключей)
2. PolynomApiService.GetAllPropertiesOfObject() → PropertyOwnerResponseCustom
   ↓
3. ModelMapper.CreateObjectWithShortProperties()
   ├─ SeparatePureValue()  → извлекает .Value.Value
   ├─ SeparatePureDescription() → извлекает описание определения свойства
   └─ SeparateEnumStringClassifId() → извлекает .Value.Description
   ↓
4. PolynomShortProperty (Value + Description + ClassifId)
   ↓
5. JsonSerializer.Serialize(objectsWithProperties)
   ↓
6. SyncUseCases.PublishAsync() → RabbitMQ
```

## Аффектированный код

- `NsiTransfer.BLL/Tools/ModelMapper.cs` — маппинг значений
- `NsiTransfer.Contract/Models/DTO/PolynomShortProperty.cs` — DTO модель
- `NsiTransfer.BLL/UseCases/SyncUseCases.cs` — передача через JSON (no changes needed, автоматически включится новое поле)

## Тестирование

Собрать и убедиться в отсутствии ошибок компиляции:

```bash
dotnet build -c Debug
```

## Связанные страницы

- [[classification]] — Классификация объектов
- [[sync-flow]] — Процесс синхронизации
- [[polynom-api]] — API Полинома
- [[http-logging]] — Логирование запросов

