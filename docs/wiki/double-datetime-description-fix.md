# Фикс: Description для Double и DateTime свойств в JSON

**Дата:** 2026-09-21  
**Статус:** ✅ Исправлено

## Проблема

При формировании JSON в бэке для понятия "Коэффициент преобразования из базовой ЕИ в" (и других свойств типа Double/DateTime) поле `Description` в Properties приходило как `null`, хотя значение существовало в Polynom API.

## Причина

В функции `SeparatePureDescription()` (ModelMapper.cs:204-273) для типов `PropertyType.Double` и `PropertyType.DateTime` использовалась неправильная логика:

```csharp
// ❌ Неправильно — ищет в Values и возвращает Value вместо Description
case PropertyType.Double:
    var doubleValue = props.Values.DoubleProperties.Value?.FirstOrDefault(...);
    return doubleValue?.Value?.Value.ToString();
```

Все остальные типы свойств (Boolean, Enum, String и т.д.) правильно ищут в `props.Definitions.*Properties` и возвращают `.Description`.

## Решение

Изменена логика для Double и DateTime на поиск в Definitions (как для остальных типов):

```csharp
// ✅ Правильно — ищет в Definitions и возвращает Description
case PropertyType.Double:
    return FindProperty(props.Definitions.DoubleProperties, 
        p => p.ObjectId == propDef.ObjectId && p.TypeId == propDef.TypeId, 
        "Definitions.DoubleProperties", propDef, obj).Description;

case PropertyType.DateTime:
    return FindProperty(props.Definitions.DateTimeProperties, 
        p => p.ObjectId == propDef.ObjectId && p.TypeId == propDef.TypeId, 
        "Definitions.DateTimeProperties", propDef, obj).Description;
```

## Файлы

- `backend/nsitransfer/NsiTransfer.BLL/Tools/ModelMapper.cs` (строки 221-227)

## Тестирование

Сборка прошла успешно. Description теперь корректно заполняется для свойств Double и DateTime в JSON, отправляемом в RabbitMQ.
