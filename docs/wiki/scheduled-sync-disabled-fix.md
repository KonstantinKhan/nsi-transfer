# Фикс: Scheduled Sync отключен (2026-08-26)

## Проблема

Периодическая синхронизация (scheduled sync) не работала. Ручной sync через API `POST /api/polynom-sync/start-data-collection-in-background` работал, но автоматический по расписанию не запускался вообще.

## Root Cause

`PolynomApiSyncBackgroundService` (hosted service, отвечающий за периодический sync) не был зарегистрирован в DI-контейнере.

**Файл:** `NsiTransfer/Program.cs:78`

```csharp
//builder.Services.AddHostedService<PolynomApiSyncBackgroundService>();  // ← закомментировано
```

**История:** Отключено в коммите `955fa89` (2026-07-24) во время работы над classification code логикой (вероятно, для локального тестирования) и забыто вернуть.

## Фикс

### 1. Добавить using в Program.cs (если не был)

**Файл:** `NsiTransfer/Program.cs:8`

```csharp
using NsiTransfer.Presentation.BackgroundServices;
```

### 2. Раскомментировать регистрацию hosted service

**Файл:** `NsiTransfer/Program.cs:78`

```csharp
// Было:
//builder.Services.AddHostedService<PolynomApiSyncBackgroundService>();

// Стало:
builder.Services.AddHostedService<PolynomApiSyncBackgroundService>();
```

### 3. Пересобрать проект

```bash
dotnet build
```

## Верификация

После применения фикса:

1. Запустить приложение
2. Проверить логи на строку:
   ```
   "Корректная конфигурация обнаружена. {name} начал синхронизацию"
   ```
   Это означает, что BackgroundService успешно стартовал

3. Дождаться интервала `StartSyncWithIntervalMinutes` (по умолчанию 15 минут) и проверить, что sync запустился по расписанию

## Вторичная проблема: Невалидный конфиг

Даже после включения hosted service, синхронизация может не запуститься, если конфиг невалиден.

**Проверяемые поля в configuration.json:**

```json
"AppConfiguration": {
  "TargetReferenceNode": {
    "TargetReferenceNodeObjectId": 0,      // ← должно быть > 0
    "TargetReferenceNodeTypeId": 0,        // ← должно быть > 0
    "TargetReferenceNodeName": ""          // ← не должна быть пустой
  },
  "EmailNotifications": {
    "ErrorRecipients": []                  // ← должна быть хотя бы одна почта
  }
}
```

Если хотя бы одно из этих полей невалидно, BackgroundService логирует info и ждет сигнала об изменении конфига. Sync не запускается.

**Решение:** Убедитесь, что реальный конфиг на среде/деплое содержит валидные значения.

## Диагностика (если всё равно не работает)

### Проверить логи по маркерам

- `"Корректная конфигурация обнаружена. {name} начал синхронизацию"` → BackgroundService стартовал OK
- `"Попытка запуска фоновой синхронизации во время выполнения другой синхронизации."` → есть активная запись в `Sendings.ActiveMarker == true` (зависшая синхронизация)
- `"Фоновый сервис синхронизации объектов из Полинома с внешней системой прервался..."` → необработанное исключение в loop

### Проверить БД на зависшие записи

```sql
SELECT * FROM "Sendings" WHERE "ActiveMarker" = true;
```

Если есть записи с `ActiveMarker = true` от старых запусков — они блокируют все scheduled runs. Нужно вручную исправить:

```sql
UPDATE "Sendings" SET "ActiveMarker" = false WHERE "ActiveMarker" = true;
```

### Проверить, что регистрация действительно сработала

В консоли при старте приложения должна быть строка про успешную регистрацию hosted services. Если её нет — check Program.cs еще раз.

## Сопутствующие материалы

- [[sync-flow]] — Полный процесс синхронизации
- [[configuration]] — Конфигурация приложения
- [[architecture]] — Архитектура BackgroundServices
