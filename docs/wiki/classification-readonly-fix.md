# Разблокировка readonly свойства при присвоении кода классификатора

**Дата:** 2026-08-27  
**Статус:** ✅ Реализовано и протестировано. ✅ Исправлена двойная отправка (2026-08-27)  
**Commits:** `3c6eaea` (readonly toggle), `e65c1be` (fix double-send)

## Проблема

При присвоении значения свойству **"Код классификатора"** из понятия **"Данные Классификатора"** возникала ошибка API ПОЛИНОМ:

```
Статус: 400 Bad Request
Ответ: "Свойство запрещено к изменению."
```

Это происходило **только** когда свойство было помечено как readonly (`isReadOnly=true`).

### Сценарий

Требуемый сценарий: свойство должно быть недоступно для редактирования пользователем в UI, но программно должно меняться синхронизацией.

## Решение

Решение основано на исследовании API ПОЛИНОМ: API поддерживает переключение флага `isReadOnly` через эндпоинт `/api/v1/concept-property-source/update`.

### Цепочка вызовов

1. **Получить свойства концепции**
   ```
   POST /api/v1/concept-property-source/get-by-concept
   Body: { objectId: <conceptId>, typeId: <conceptTypeId> }
   Response: ConceptPropertySource[] (массив с полями id, name, isReadOnly, isReadOnlyEnabled, ...)
   ```

2. **Разблокировать свойство**
   ```
   POST /api/v1/concept-property-source/update
   Body: { conceptPropertySource: { objectId: <id>, typeId: <typeId> }, isReadOnly: false }
   Response: 200 OK
   ```

3. **Записать значение** (уже существующий механизм)
   ```
   POST /api/v1/property-owner/set-property-values
   Body: SetPropertyValuesRequest {...}
   Response: 200 OK
   ```

4. **Вернуть блокировку** (в блоке finally)
   ```
   POST /api/v1/concept-property-source/update
   Body: { conceptPropertySource: { objectId: <id>, typeId: <typeId> }, isReadOnly: true }
   Response: 200 OK
   ```

## Реализация

### Изменённые файлы

#### 1. `NsiTransfer.DAL/Routes/ApiRoutes.cs`
```csharp
public static string ConceptPropertySourceByConceptId => Path("/concept-property-source/get-by-concept");
public static string ConceptPropertySourceUpdate => Path("/concept-property-source/update");
```

#### 2. `NsiTransfer.DAL/Interfaces/Http/IPolynomApiHttpRepository.cs`
```csharp
Task<Result<List<ConceptPropertySource>>> GetConceptPropertiesByConceptId(int conceptObjectId, int conceptTypeId, CancellationToken cancellationToken);
Task<Result<bool>> UpdateConceptPropertySource(int propertySourceId, int typeId, bool isReadOnly, CancellationToken cancellationToken);
```

#### 3. `NsiTransfer.DAL/Repositories/Network/PolynomApiHttpRepository.cs`
- **GetConceptPropertiesByConceptId** — отправляет `{ objectId, typeId }` на эндпоинт get-by-concept
- **UpdateConceptPropertySource** — отправляет `{ conceptPropertySource: { objectId, typeId }, isReadOnly }` на эндпоинт update

#### 4. `NsiTransfer.BLL/Services/PolynomApiService.cs`
Метод `UpdateClassificationCodeAsync` оборачивает логику SetPropertyValuesOfPropertyOwner:

```csharp
// Получить свойства концепции
var propertiesResult = await _apiRepository.GetConceptPropertiesByConceptId(
    editedContract.ObjectId, (int)editedContract.TypeId, cancellationToken);

// Найти нужное свойство по имени
var propertySource = propertiesResult.Data?.FirstOrDefault(p =>
    p.Name.Equals(editedPropertyInContract.Name, StringComparison.OrdinalIgnoreCase));

// Проверить можно ли разблокировать
if (propertySource.IsReadOnly && propertySource.IsReadOnlyEnabled)
{
    // Разблокировать
    var unlockResult = await _apiRepository.UpdateConceptPropertySource(
        propertySource.ObjectId, (int)propertySource.TypeId, false, cancellationToken);
    if (unlockResult.IsSuccess) wasUnlocked = true;
}

try
{
    // Записать значение (оригинальная логика)
    return await _apiRepository.SetPropertyValuesOfPropertyOwner(request, cancellationToken);
}
finally
{
    // Вернуть блокировку
    if (wasUnlocked)
    {
        await _apiRepository.UpdateConceptPropertySource(
            propertySourceId, (int)propertySourceTypeId, true, cancellationToken);
    }
}
```

## Ключевые моменты

### Проверка isReadOnlyEnabled
API может вернуть флаг `isReadOnlyEnabled=false`, что означает сам флаг isReadOnly заблокирован и не может быть изменён. В этом случае попытка разблокировки вернёт ошибку. Код проверяет оба условия:

```csharp
if (propertySource.IsReadOnly && propertySource.IsReadOnlyEnabled)
```

### Типы ObjectId и TypeId
Важно передавать **оба** параметра при обновлении:
- `objectId` — числовой идентификатор свойства-источника (176690)
- `typeId` — тип свойства-источника (54 = ConceptPropertySource)

API возвращает оба в ответе get-by-concept.

### Окно гонки
Флаг разблокировки меняется **глобально** для концепции, не для одного элемента. Между Шагом 2 (разблокировка) и Шагом 4 (блокировка) свойство редактируемо всеми пользователями. Поэтому:
- Пауза должна быть минимальной (WriteAsync сетевой операции)
- Блокировка вернётся в finally для гарантии даже при ошибке

## Логирование

Добавлено подробное логирование для отладки:
- Получение свойств с деталями каждого
- Нахождение нужного свойства
- Попытка разблокировки и её результат
- Результат записи значения
- Возврат блокировки

Логи помогают диагностировать проблемы, если isReadOnly не переключается или запись не удаётся.

## Результаты тестирования

✅ Объект успешно получает код классификатора  
✅ Разблокировка происходит перед записью  
✅ Блокировка восстанавливается после записи  
✅ При ошибке записи блокировка всё равно восстанавливается (finally)

## Исправление: Двойная отправка при присвоении кода (Commit: e65c1be)

**Проблема:** После присвоения кода классификатора объект повторно отправляется в следующей синхронизации.

**Корень проблемы:** 
- При присвоении кода вызывается `SetPropertyValuesOfPropertyOwner`, который обновляет `lastModified` объекта в ПОЛИНОМ
- Sync использовал `InitiatedAt` (время НАЧАЛА) предыдущей синхронизации для вычисления startTime
- Следующая Sync начиналась в то же время (InitiatedAt), поэтому `GetDiffsInTimePeriod` переподхватывал объект с обновленным `lastModified`
- Объект отправлялся повторно

**Решение:** Три изменения в `SyncUseCases.cs` для вычисления периода синхронизации:

1. **Использовать EndedAt вместо InitiatedAt** (строка 363)
   - startTime следующей Sync = `EndedAt` предыдущей (когда она закончилась)
   - Гарантирует что Sync2 начинается ПОСЛЕ завершения Sync1

2. **Отсортировать по EndedAt при поиске последней Sync** (строка 360)
   - Если syncs завершаются не в порядке запуска, orderBy по EndedAt выбирает правильный "последний"
   - Иначе может быть выбрана более ранняя Sync

3. **Округлить вверх вместо вниз** (строка 364)
   - Старый код: `AddTicks(-(rawTime.Ticks % TimeSpan.TicksPerSecond))` — округляет вниз
   - Новый: `AddTicks((TimeSpan.TicksPerSecond - (rawTime.Ticks % TimeSpan.TicksPerSecond)) % TimeSpan.TicksPerSecond)` — округляет вверх
   - Избегает пересечения: объекты измененные в диапазоне [rounded_down, original] не будут переподхвачены

**Результат:** Объект, измененный во время Sync1 с lastModified < EndedAt(Sync1), не будет подхвачен в Sync2 (которая начинается с EndedAt(Sync1)).

## Связанные статьи

- [[classification-code-processor]] — основной процессор кодов
- [[polynom-api]] — API ПОЛИНОМ
- [[sync-flow]] — процесс синхронизации
