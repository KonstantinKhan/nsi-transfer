# Resume: скриншоты NsiTransfer

Контекст для новой opencode-сессии после рестарта (MCP playwright подключён).

## Что уже сделано (Фаза 0 + prep)

- MCP `playwright` прописан в `/root/.config/opencode/opencode.jsonc`:
  `["npx","-y","@playwright/mcp@latest","--headless","--isolated"]`
- Chromium браузер скачан: `/root/.cache/ms-playwright/chromium-1228`
- Фронт: `npm install` выполнен в `frontend/nsitransferfront/`
- Бэк: `dotnet restore NsiTransfer/NsiTransfer.csproj` выполнен (dotnet 8.0.421, `.slnx` НЕ поддерживается — ресторить только по csproj)

## Параметры запуска

- Бэк: `cd backend/nsitransfer && dotnet run --project NsiTransfer/NsiTransfer.csproj --launch-profile http` → порт **5177** (профиль `http` в `Properties/launchSettings.json`)
- Фронт: `cd frontend/nsitransferfront && npm run dev` → порт **5173**
- Логин/пароль: **admin / 123** (из `PolynomAuthCreds` в launchSettings.json)
- RabbitMQ `localhost:5672` (guest/guest) и ПОЛИНОМ `172.23.14.181:5100` — пользователь поднимает сам

## Навигация UI (роутер `frontend/nsitransferfront/src/router/index.ts`)

- `/login` — поля `Логин`, `Пароль`, кнопка `Войти`
- `/main` → SyncView: кнопка `Начать синхронизацию` + история (`SendingStatistics` карточки)
- `/main/config` — меню 4 секций
- `/main/config/target-node` — **`Настройка целевого справочника`**, radio-список первого слоя из ПОЛИНОМ (`getFirstLayerReferences`). Пункт **«Коды»** = целевой справочник = область поиска. Кнопка `Сохранить`.
- `/main/config/rabbitmq` | `/polynom` | `/email`

«Область поиска» и «целевой справочник Коды» — одно и то же поле (`TargetNodeView.vue:11`).

## Сценарии скринов → файлы в `docs/screenshots/`

1. `01-login-empty.png` — `/login` пустой
2. `02-login-error.png` — плохие креды → `error-message`
3. `03-sync-empty.png` — `/main`, `Нет данных о синхронизациях`
4. `04-config-menu.png` — `/main/config`, 4 секции
5. `05-target-node-select.png` — radio-список, **«Коды»** выбран (до сохранения)
6. `06-target-node-saved.png` — после `Сохранить`, `StatusMessage` success
7. `07-config-rabbitmq.png`, `08-config-polynom.png`, `09-config-email.png`
8. `10-sync-running.png` — во время `isSyncing` (`Запуск...` + спиннер)
9. `11-sync-completed.png` — зелёный бейдж `Completed`
10. `12-sending-expanded.png` — карточка раскрыта (сообщения)
11. `13-sync-error.png` — красный бейдж + `failure-block`. **Требует реальный сбой**: стоп RabbitMQ mid-sync, либо использовать существующие failed-записи.

## Порядок действий после рестарта

1. healthcheck: запустить бэк+фронт, curl `http://localhost:5177/api/configuration` и корень `http://localhost:5173`
2. проверить что `getFirstLayerReferences` возвращает «Коды»: GET `http://localhost:5177/api/configuration/...` (точный эндпоинт см. `configurationService.ts`)
3. через playwright MCP: navigate → click → type → take_screenshot по сценарию выше
4. финал: `ls docs/screenshots/` сверка 13 файлов

## Риски

- Сценарий 11 (error) — нужен реальный сбой синхронизации
- Сценарий 9 (completed) — если Polynom отдаёт много объектов, ждать SSE `SendingCompleted` (поллинг snapshot)
- `dotnet run` через `.slnx` не запустится — только по csproj
