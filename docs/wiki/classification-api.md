# API Полинома для работы с классификацией

Специализированные эндпоинты API Полинома для получения и обновления кодов классификатора.

## Основные операции

### 1. Получение корневого узла классификации

**Эндпоинт:** `POST /api/v1/tree/get-classification`

**Метод:** `PolynomApiService.GetClassificationRootNode()`

**Запрос:**

```json
{
  "FilterOptions": "Name",
  "FilterString": "",
  "Options": 136,
  "PageNumber": 0,
  "PageSize": 0
}
```

**Ответ:**

```json
{
  "Items": [
    {
      "NodeObject": {
        "ObjectId": 1,
        "TypeId": 203
      },
      "NodeName": "Все справочники",
      "ItemsCount": 50
    }
  ],
  "HasNextPage": false,
  "PageNumber": 1,
  "PageSize": 1
}
```

### 2. Получение дочерних узлов классификации

**Эндпоинт:** `POST /api/v1/tree/get-classification-node-children`

**Метод:** `PolynomApiService.GetClassificationChildrenNodes()`

**Запрос:**

```json
{
  "ParentNodeObject": {
    "ObjectId": 61,
    "TypeId": 48
  },
  "FilterOptions": "Name",
  "FilterString": "",
  "Options": 410,
  "PageNumber": 1,
  "PageSize": 100
}
```

**Ответ:**

```json
{
  "Items": [
    {
      "NodeObject": {"ObjectId": 123, "TypeId": 48},
      "NodeName": "Система кодирования А",
      "ItemsCount": 5
    },
    {
      "NodeObject": {"ObjectId": 124, "TypeId": 48},
      "NodeName": "Система кодирования Б",
      "ItemsCount": 3
    }
  ],
  "HasNextPage": false,
  "PageNumber": 1,
  "PageSize": 100
}
```

### 3. Получение родительских групп

**Эндпоинт:** `POST /api/v1/classification-object/get-parent-groups`

**Метод:** `PolynomApiService.GetParentGroups()`

**Запрос:**

```json
{
  "ObjectId": 456,
  "TypeId": 45
}
```

**Ответ:**

```json
[
  {
    "ObjectId": 123,
    "TypeId": 48,
    "Name": "Группа 1"
  },
  {
    "ObjectId": 124,
    "TypeId": 48,
    "Name": "Группа 2"
  }
]
```

### 4. Получение свойств объекта

**Эндпоинт:** `POST /api/v1/property-owner/get-properties`

**Метод:** `PolynomApiService.GetAllPropertiesOfObject()`

**Запрос:**

```json
{
  "Owner": {
    "ObjectId": 456,
    "TypeId": 45
  }
}
```

**Ответ:**

```json
{
  "Owner": {"ObjectId": 456, "TypeId": 45},
  "Contracts": [
    {
      "ObjectId": 789,
      "TypeId": 67,
      "Name": "Данные классификатора",
      "Properties": [
        {
          "ObjectId": 100,
          "TypeId": 78,
          "Name": "Код классификатора",
          "Value": "001"
        },
        {
          "ObjectId": 101,
          "TypeId": 78,
          "Name": "Минимальное значение кода",
          "Value": "001"
        }
      ]
    }
  ]
}
```

### 5. Установка кода классификатора

**Эндпоинт:** `POST /api/v1/property-owner/set-property-values`

**Метод:** `PolynomApiService.UpdateClassificationCodeAsync()`

**Запрос:**

```json
{
  "Owner": {
    "ObjectId": 456,
    "TypeId": 45
  },
  "Properties": [
    {
      "Contract": {
        "ObjectId": 789,
        "TypeId": 67
      },
      "Definition": {
        "ObjectId": 100,
        "TypeId": 78
      },
      "Value": "004",
      "EvaluationMode": 0
    }
  ],
  "Values": {
    "StringProperties": [
      {
        "Value": "004",
        "ObjectId": 1,
        "TypeId": 0
      }
    ]
  },
  "AddedOwnConcepts": [],
  "DeletedDynamicProperties": [],
  "DeletedOwnConcepts": [],
  "DeletedOwnProperties": []
}
```

**Ответ:**

```json
{
  "Success": true,
  "Message": "Property values set successfully",
  "ProcessedCount": 1
}
```

## Обработка классификации в коде

### Получение информации о классификации

```csharp
// 1. Получить корневой справочник
var rootResult = await _polynomApiService.GetClassificationRootNode(cancellationToken);

// 2. Получить дочерние элементы
var childrenResult = await _polynomApiService.GetClassificationChildrenNodes(
    rootNode: rootResult.Data,
    cancellationToken);

// 3. Для каждого элемента получить свойства
foreach (var node in childrenResult.Data)
{
    var propsResult = await _polynomApiService.GetAllPropertiesOfObject(
        objectId: node.NodeObject.ObjectId,
        typeId: node.NodeObject.TypeId,
        cancellationToken);
}
```

### Установка кода классификатора

```csharp
// 1. Получить родительские группы
var parentGroupsResult = await _polynomApiService.GetParentGroupsWithProperties(
    objectId: targetObject.ObjectId,
    typeId: targetObject.TypeId,
    cancellationToken);

// 2. Определить группу с кодами
var groupWithCodes = parentGroupsResult.Data.FirstOrDefault(HasClassificationCodeProperty);

// 3. Получить максимальный код в группе
var maxCodeResult = await _polynomApiService.GetLastClassificationCodeInGroup(
    groupObjectId: groupWithCodes.ObjectId,
    groupTypeId: groupWithCodes.TypeId,
    cancellationToken);

// 4. Установить новый код
var newCode = IncrementCode(maxCodeResult.Data);
var updateResult = await _polynomApiService.UpdateClassificationCodeAsync(
    updatedModel: targetObject,
    editedContract: classificationContract,
    editedPropertyInContract: codeProperty.WithNewValue(newCode),
    cancellationToken);
```

## Опции классификации (Options)

**Значение Options:** 410 (для дочерних узлов) или 136 (для корня)

Битовые флаги:
- `ShowViewpoints` — показывать viewpoints (128)
- `ShowViewpointCatalog` — показывать каталоги viewpoint (256)
- `ShowDocuments` — показывать документы (16)
- `ShowDocumentCatalog` — показывать каталоги документов (64)
- `ShowElements` — показывать элементы (2)

## Связанные страницы

- [[classification]] — Система классификации
- [[classification-code-processor]] — Обработчик кодов
- [[polynom-api]] — Полином API (общее)
- [[http-logging]] — Логирование запросов

