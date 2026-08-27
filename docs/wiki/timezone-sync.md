# Синхронизация данных и часовые пояса

Конфигурация часового пояса критична для корректной работы синхронизации данных из Полинома. Ошибка в `PolynomConfig__TimeZoneId` приведёт к сдвигу временного диапазона и потере синхронизированных объектов.

## Как работает конвертация времени

### Внутреннее хранилище (NsiTransfer)

- Все даты в БД и коде хранятся в **UTC** (`DateTime.UtcNow`, `DateTime.Kind == DateTimeKind.Utc`).
- PostgreSQL колонки типизированы как `timestamp with time zone` (timestamptz) — гарантирует сохранение абсолютного момента времени независимо от TZ сервера.
- В `SyncUseCases.GetSearchDateTimePeriod()` окно синхронизации `[From, To]` формируется в UTC.

### Отправка запроса к Полиному

1. Перед отправкой в API Полинома окно конвертируется методом `PolynomTimeConverter.ConvertToPolynomTime()` (файл `NsiTransfer.BLL/Tools/PolynnomTimeConverter.cs`).
2. UTC → **наивное локальное время** сервера Полинома:
   ```csharp
   var localTime = TimeZoneInfo.ConvertTimeFromUtc(utcDateTime, 
       TimeZoneInfo.FindSystemTimeZoneById(timeZoneId));
   return DateTime.SpecifyKind(localTime, DateTimeKind.Unspecified);
   ```
3. Наивное время (без информации о TZ) отправляется в запросе `dateTimeProperties[0]` (From) и `dateTimeProperties[1]` (To).
4. Полином ожидает именно **локальное время машины, на которой установлен его сервер**.

### Источник конфигурации TimeZoneId

`PolynomConfig__TimeZoneId` читается из:
1. `launchSettings.json` (`Ekaterinburg Standard Time`, для dev F5)
2. Переменные окружения (переопределяют launchSettings)
3. `.env` файл (используется Docker, не используется при F5)

**Порядок приоритета для dev-режима (F5 в VS Code):**
- `launchSettings.json` → переменные окружения в профиле

## Типичная проблема: неправильный TimeZoneId

**Симптом:** время в запросе к Полиному на N часов раньше реального времени сервера Полинома → синхронизация не находит новых объектов.

**Пример:** 
- Настроено: `Russian Standard Time` (UTC+3)
- Реально: сервер Полинома в UTC+5 (Екатеринбург)
- Результат: запрос отправляется с временем UTC+3, а Полином ожидает UTC+5 → сдвиг на 2 часа.

**Список Windows TZ ID для России:**
- `Russian Standard Time` — UTC+3 (Москва, Санкт-Петербург)
- `Ekaterinburg Standard Time` — UTC+5 (Екатеринбург)
- `Altai Standard Time` — UTC+7 (Новосибирск, Красноярск)
- `Magadan Standard Time` — UTC+11 (Магадан)
- Прочие — см. [Microsoft Docs: IANA Time Zone Mapping](https://docs.microsoft.com/en-us/windows-hardware/manufacture/desktop/default-time-zones)

## Как исправить

1. **Уточнить фактический часовой пояс сервера Полинома** — на машине, где установлен Полином, выполнить `date` (Linux) или `Get-TimeZone` (Windows).

2. **Найти соответствующий Windows TZ ID** — например, если сервер в UTC+5, то `Ekaterinburg Standard Time`.

3. **Обновить конфигурацию:**
   - **Для dev-режима:** измени `NsiTransfer/Properties/launchSettings.json` профиль `http`:
     ```json
     "PolynomConfig__TimeZoneId": "Ekaterinburg Standard Time"
     ```
   - **Для Docker/продакшена:** обнови `.env` (корень проекта):
     ```
     PolynomConfig__TimeZoneId=Ekaterinburg Standard Time
     ```

4. **Полный перезапуск приложения:**
   - Остановить приложение (Ctrl+Shift+D в VS Code или остановить контейнер).
   - **Удалить `bin/Debug` полностью** — это критично, так как конфиг копируется при компиляции.
   - Запустить F5 заново (или `docker compose up -d`).

## Отладка

**Проверить отправленное время в логах:**

В логе HTTP запроса (см. [[http-logging]]) найди REQUEST BODY для `POST /api/v1/search/execute-property-search`:
```json
{
  "values": {
    "dateTimeProperties": [
      {"objectId": 0, "value": {"value": "2026-08-25T13:52:59"}},
      {"objectId": 1, "value": {"value": "2026-08-25T15:55:12"}}
    ]
  }
}
```

Сверь `dateTimeProperties[1].value.value` (то время, которое отправлено как "To") с реальным временем на сервере Полинома. Должны совпадать с точностью до минуты.

**Проверить, прошла ли синхронизация:**

После запроса посмотри на таблицу `PolynomObjects` — должны появиться объекты с `ProcessedAt` в момент синхронизации. Если таблица пуста, значит Полином не нашёл изменений в окне `[From, To]`.

## Связанные страницы

- [[sync-flow]] — Полный процесс синхронизации
- [[polynom-api]] — Конфигурация Полинома
- [[http-logging]] — Как смотреть логи HTTP запросов
