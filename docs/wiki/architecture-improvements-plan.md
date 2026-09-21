# План развития архитектуры (сентябрь 2026)

Анализ 4 архитектурных проблем + варианты решения + рекомендации. Собрано workflow из 7 агентов (3 investigate + 4 design, 2026-09-07). Это план, не факт реализации — статус каждого пункта см. ниже.

## Статус

| # | Проблема | Статус |
|---|----------|--------|
| 3 | Персистентный кеш максимума кода группы + кнопка индексации | реализовано 2026-09-07, см. [[classification-code-processor]] |
| 4 | Переиспользование свободных номеров кода после удаления | реализован этап 1 (Lazy Search) 2026-09-07, см. [[classification-code-processor]]. Этап 2 (интеграция с персистентным кешем) не реализован |
| 2 | Информация о группе в JSON объекта | реализовано 2026-09-07, см. [[classification-code-processor]] |
| 1 | Переход на отдельные jsonb-строки вместо массива в Message | реализовано 2026-09-07 (только новые данные, без миграции старых — решение пользователя), см. [[sync-flow]] |

Порядок реализации выбран пользователем: 3 → 4 → 2 → 1.

---

## Проблема 1: хранение сообщений (массив в одной jsonb-строке)

**Текущее состояние:** `Message.SerializedMessage` (`NsiTransfer.DAL/Db/Entities/Message.cs`) хранит весь массив объектов одной jsonb-строкой в таблице `Messages`. Собирается в `SyncUseCases.cs:655` (основной цикл) и `SyncUseCases.cs:866` (retry), публикуется в RabbitMQ через `RabbitMqPublisher.cs:126`. При десятках тысяч элементов: pgAdmin неудобен, JSON-запрос находит весь массив целиком, нет индексов на отдельные элементы, нет параллельной обработки.

**Варианты:**
1. **Таблица `MessageObjects`** (рекомендовано) — каждый объект отдельной строкой (Id, MessageId FK, объект как jsonb, индексы на ObjectId/TypeId/Name). `SerializedMessage` становится кешем/переходным слоем; при отправке в RabbitMQ собирается `SELECT` по MessageId. Паттерн аналогичен уже существующей `PolynomObjects` (хранит успешные объекты отдельно).
2. Гибрид — `BatchId` колонка + отдельная индекс-таблица `MessageObjectsIndex`, без переделки основного потока отправки. Меньше риска, но дублирование данных.
3. Архивирование — новые Message в новую таблицу, старые не трогать. Не решает проблему на будущее, техдолг.

**Рекомендация:** вариант 1. Точки изменения: новый Entity `MessageObject.cs`, `AppDbContext.cs` (DbSet), `SyncUseCases.cs:520-550,655,746,866`, `RabbitMqPublisher.cs:126` (сборка JSON из MessageObjects перед публикацией), скрипт миграции существующих `SerializedMessage` → `MessageObjects`.

---

## Проблема 2: группа классификатора в JSON объекта

**Текущее состояние:** модель `PolynomObjectWithShortProperties` (`NsiTransfer.Contract/Models/DTO/`) группу не содержит вообще. Данные о группе (min/max код — свойства "Минимальное/Максимальное значение кода") уже достаются через `GetParentGroupsWithProperties` (`PolynomApiService.cs:124-155`, используется в `ClassificationCodeProcessor.cs:67,107`), но только для внутренней валидации, наружу не передаются. Полный путь группы до корня Polynom API одним вызовом не даёт — нужны рекурсивные `GetParentGroups`.

**Варианты:**
1. Минимальный `GroupInfo{ObjectId,TypeId,Name}` — дёшево, но получателю всё равно нужны доп. запросы за диапазоном кодов.
2. **Стандартный `GroupInfo{ObjectId,TypeId,Name,MinCode,MaxCode,IsLeaf}`** (рекомендовано) — переиспользует уже имеющийся вызов `GetParentGroupsWithProperties`, кешируется per sync run по паттерну `_groupLastCodeCache`. +150-200 байт на объект.
3. Расширенный с полным путём до корня — даёт максимум диагностики, но требует рекурсивных вызовов на каждый уровень иерархии, +500 байт/объект, риск замедления sync. Отклонено.

**Рекомендация:** вариант 2. Точка встройки — `ModelMapper.CreateObjectWithShortProperties` (`NsiTransfer.BLL/Tools/ModelMapper.cs:11-38`). Min/Max property names уже есть в конфиге (`MinCodePropertyName`/`MaxCodePropertyName`, `PolynomApiSyncOptions`).

---

## Проблема 3: персистентный кеш максимума кода группы + кнопка индексации

**Текущее состояние:** `_groupLastCodeCache` (`ClassificationCodeProcessor.cs:22-25`) — Dictionary на Scoped процессоре, живёт один sync run, не персистентный между запусками. Группы вообще не хранятся локально — только в Polynom. Обход большой группы = ~505 HTTP запросов (пагинация по 100 + запрос свойств на каждого child в `GetLastClassificationCodeInGroup`, `PolynomApiService.cs:165-214`).

**Варианты:**
1. **Простая персистентная таблица `ClassificationGroupCodeMax(GroupObjectId, GroupTypeId, LastMaxCode, UpdatedAt)`** (рекомендовано) — уникальный констрейнт на (GroupObjectId, GroupTypeId). При старте sync — один SELECT грузит всё в память вместо сотен запросов к Polynom. После обработки — batch upsert. Реконсиляция: если реальный max из Polynom API больше кешированного (первый обход после деплоя, ручные изменения в Polynom) — берём реальный.
2. Версионированное состояние committed/reserved с process id — сложнее, оверкилл для текущей Scoped/последовательной архитектуры (параллельные sync сейчас запрещены).
3. Журнал истории аллокаций — растёт линейно с числом объектов, не решает первопричину (обход Polynom).
4. Статус-кво (без изменений) — 505 запросов на каждый sync run для больших групп, неприемлемо.

**Рекомендация:** вариант 1.

**Реализация:**
1. Entity `ClassificationGroupCodeMax` (Id, GroupObjectId, GroupTypeId, LastMaxCode, UpdatedAt), UC на (GroupObjectId, GroupTypeId)
2. `DbSet<ClassificationGroupCodeMax>` в `AppDbContext`, EF миграция
3. `ClassificationCodeProcessor` при создании (Scoped) грузит таблицу в `_groupLastCodeCache` вместо пустого Dictionary
4. При miss в кеше — старое поведение (API обход), затем резерв в Dictionary
5. После успешной обработки — batch upsert изменённых (GroupId, TypeId, NewCode) в БД
6. Кнопка индексации — `POST /api/polynom-sync/rebuild-group-code-cache`, обходит все целевые группы один раз через `GetLastClassificationCodeInGroup`, bulk-upsert в таблицу. Долгая операция (5-30 мин на 1000 групп) — фон + прогресс

---

## Проблема 4: переиспользование свободных номеров после удаления из середины группы

**Текущее состояние:** дыры не переиспользуются — код всегда растёт от максимума. При `newCode > maxValue` — `InvalidOperationException` (`ClassificationCodeProcessor.cs:243-247`), объект падает в `PolynomObjectFailure`, никакого fallback на поиск свободных номеров.

**Варианты:**
1. Eager Search (искать дыры всегда) — отклонено, замедление ~250x на обычном сценарии (25 групп × 505 запросов вместо 25 × 5).
2. **Lazy Search** (рекомендовано, этап 1) — обычный путь (increment) как сейчас; при исчерпании диапазона — catch `InvalidOperationException` → полный обход группы, найти первый свободный номер. Защита от повторных обходов в одном run — кешировать весь Set найденных свободных номеров (`_groupFreeMappingCache`), не только один.
3. Persistent Cache отдельно от проблемы 3 — дублирует её, не нужно как отдельный вариант.
4. **Гибрид Lazy Search + Persistent Cache** (рекомендовано, этап 2, после реализации проблемы 3) — персистентный кеш даёт стартовую точку для инкремента, Lazy Search — только fallback при реальном исчерпании диапазона. Найденный свободный номер пишется обратно в `ClassificationGroupCodeMax`, следующий run стартует с него.

**Рекомендация:** этап 1 — Lazy Search сейчас (независимо от проблемы 3, малый риск); этап 2 — после проблемы 3, интеграция с персистентным кешем.

---

## Связанные страницы

- [[classification-code-processor]] — текущий процессор кодов (основа для проблем 3, 4)
- [[classification]] — система классификации
- [[classification-api]] — API методы
- [[sync-flow]] — процесс синхронизации
- [[polynom-api]] — Polynom API эндпоинты
