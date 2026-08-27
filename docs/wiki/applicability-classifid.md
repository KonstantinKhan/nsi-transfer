# Applicability (Применяемость) ClassifId Override

**Дата:** 2026-08-27  
**Компонент:** `ModelMapper.cs` / Broker message ClassifId field  
**Статус:** Реализовано

## Проблема

При отправке свойства "Применяемость" (тип `EnumString`) в RabbitMQ брокер, поле `ClassifId` получало значение из `Description` поля enum элемента Polynom API. Это не соответствует требуемой логике, где `ClassifId` должен зависеть от текстового значения свойства:

- "Разрешен к применению" → ClassifId **"0"**
- "Запрещен к применению" → ClassifId **"1"**
- "Ограничено разрешен" → ClassifId **"1"**

## Решение

**Файл:** `NsiTransfer.BLL/Tools/ModelMapper.cs`  
**Метод:** `CreateShortProperties()` (строки 58-76)

Добавлена специальная логика для переопределения `ClassifId` на основе имени свойства и его значения. После получения значений через `GetValueAndDescription()` (line 59), перед построением `PolynomShortProperty` DTO (line 61), вставлена проверка:

```csharp
if (string.Equals(propName, "Применяемость", StringComparison.OrdinalIgnoreCase))
{
    classifId = value switch
    {
        "Разрешен к применению" => "0",
        "Запрещен к применению" => "1",
        "Ограничено разрешен" => "1",
        _ => classifId
    };
}
```

Логика использует уже доступные в scope переменные `propName` (из `prop.Name`) и `value` (декомпозиция кортежа из `GetValueAndDescription`). Для прочих значений и прочих EnumString-свойств сохраняется существующее поведение (используется Description из Polynom API).

## Маршрут данных

1. `CreateShortProperties()` iterates через свойства контракта
2. Каждое свойство проходит через `GetValueAndDescription()`, возвращающий `(Value, Description, PropertyType, ClassifId)`
3. **NEW:** Если свойство именуется "Применяемость", переопределяется ClassifId на основе Value текста
4. Построенный `PolynomShortProperty` DTO содержит финальный ClassifId
5. JSON сериализуется и отправляется в RabbitMQ через `SyncUseCases.PublishAsync()`

## Тестирование

- ✓ Компиляция: `dotnet build --configuration Release` — успешно, 0 ошибок, 0 предупреждений
- Функциональное тестирование: пользователь проверяет корректность через интеграционные тесты синхронизации

## Связанное

- [[enum-string-mapping]] — Более ранний фикс для корректного извлечения Value и Description из enum элементов
- [[classification-code-processor]] — Аналогичная логика переопределения свойств по имени (существующий паттерн в проекте)
