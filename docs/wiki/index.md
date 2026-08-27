# NsiTransfer Wiki

Полное руководство по архитектуре, конфигурации и компонентам системы NsiTransfer.

## Содержание

### Система и конфигурация
- [[configuration]] — Конфигурация приложения (launchSettings.json, .env, configuration.json)
- [[rabbitmq]] — Установка и конфигурация RabbitMQ брокера сообщений
- [[environment]] — Переменные окружения и их назначение
- [[timezone-sync]] — Синхронизация и часовые пояса (PolynomConfig__TimeZoneId, отладка)
- [[scheduled-sync-disabled-fix]] — Фикс: BackgroundService для периодического sync был отключен (2026-08-26)

### API и интеграция
- [[polynom-api]] — Полином API: эндпоинты, запросы и ответы
- [[http-logging]] — Логирование HTTP запросов к внешним сервисам
- [[sync-flow]] — Процесс синхронизации данных из Полинома

### Классификация объектов
- [[classification]] — Система классификации объектов
- [[classification-code-processor]] — Обработчик кодов классификатора
- [[classification-api]] — API методы для работы с классификацией
- [[enum-string-mapping]] — Маппинг значений EnumString и классификационные ID
- [[applicability-classifid]] — Override ClassifId для свойства "Применяемость" по его значению
- [[leaf-group-check]] — Проверка конечной группы и страховка от удалённых объектов

### Архитектура
- [[architecture]] — Архитектура приложения
- [[project-structure]] — Структура проекта и организация кода

---

**Последнее обновление:** 2026-08-27 (3) — кеш максимума кода группы в ClassificationCodeProcessor, устраняет N полных обходов группы при массовом создании объектов (N объектов одной группы → 1 обход вместо N). Кеш — простой Dictionary-field на Scoped-процессоре (новый экземпляр на sync run, обработка строго последовательная) без блокировок. `_groupLastCodeCache` в `ClassificationCodeProcessor.cs:22-25`, чтение/резервирование в `HandleOneGroupAsync` (`ClassificationCodeProcessor.cs:192-231`). Подтверждено пользователем. [[classification-code-processor]]

**Предыдущее обновление:** 2026-08-27 (2) — добавлена проверка "объект в конечной группе" и страховка от удалённых объектов. (1) Новая проверка `IsGroupLeafAsync()` в ClassificationCodeProcessor: перед обработкой кода запрашиваются подгруппы через `/api/v1/element-group/get-by-group`, если найдены → failure `GroupIsNotLeaf`. (2) В RetryFailedObjectsAsync добавлены две проверки: при 404 (объект удалён) и при пустых контрактах (объект повреждён) — запись удаляется из PolynomObjectFailures и объект не отправляется в брокер. Логирование для обоих случаев. [[leaf-group-check]]

**Предыдущее обновление:** 2026-08-27 — добавлена проверка `CanUnassign` в `RetryFailedObjectsAsync`. Объекты, которые имеют понятие "Данные Классификатора", но не входят ни в одну группу справочника Классификатора (CanUnassign=true от API), теперь обрабатываются корректно: если код классификатора пустой — запись удаляется из PolynomObjectFailures; если код не пустой — отправляется email-уведомление оператору (объект осиротел вне групп) и ProcessAsync не вызывается (прерывается бесполезный цикл повторов). [[sync-flow]] — раздел "Повторная обработка ошибочных объектов (RetryFailedObjectsAsync)".

**Предыдущее обновление:** 2026-08-27 — добавлена логика переопределения ClassifId для свойства "Применяемость" на основе его значения. Вместо использования `Description` из Polynom API, ClassifId теперь явно маппируется: "Разрешен к применению" → "0", "Запрещен к применению" / "Ограничено разрешен" → "1". Реализовано в `ModelMapper.CreateShortProperties()` без изменения сигнатур вспомогательных методов. [[applicability-classifid]]

**Предыдущее обновление:** 2026-08-26 (3) — re-enabled scheduled sync. `PolynomApiSyncBackgroundService` (hosted service для периодической синхронизации) был отключен (закомментирована регистрация в `Program.cs`). Возобновлена работа: добавлена регистрация `AddHostedService<PolynomApiSyncBackgroundService>()` и импорт `using NsiTransfer.Presentation.BackgroundServices`. Периодический sync теперь будет запускаться по интервалу `StartSyncWithIntervalMinutes` (по умолчанию 15 минут из configuration.json). **Важно:** убедитесь, что конфиг содержит валидные `TargetReferenceNode` (ObjectId, TypeId, Name) и `EmailNotifications.ErrorRecipients` — иначе сервис ждёт валидного конфига и sync не запустится.

**Предыдущее обновление:** 2026-08-26 (2) — исправлена маршрутизация значений EnumString: `Value` теперь содержит реальное значение (например, "Вспомогательные материалы"), добавлено опциональное поле `ClassifId` для хранения классификационного кода из описания enum элемента (например, "AM"). Исправлена аналогичная ошибка для `EnumBool`, `EnumInt`, `EnumDouble`.

**Ещё ранее:** 2026-08-26 — исправлен баг: записи об ошибках удаляются из `PolynomObjectFailures` при успешной обработке объекта в основном цикле синхронизации. Добавлены детали управления жизненным циклом ошибочных объектов

**Версия приложения:** .NET 8.0  
**Фреймворк:** ASP.NET Core

