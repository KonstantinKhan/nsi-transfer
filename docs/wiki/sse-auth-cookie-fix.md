# Фикс: 401 на SSE и cookie-авторизация по HTTP (2026-09-23)

## Проблема

После авторизации в UI на HTTP-деплое (`http://10.54.188.90:8080`) все авторизованные запросы падали с 401. Особенно заметен был SSE-эндпоинт `GET /api/polynom-sync/listen-for-all-sync-events` — консоль заполнялась ошибками `EventSource`, «Ошибка общего SSE-соединения».

В dev на `localhost` всё работало — баг проявился только в проде.

## Root Cause

Каскад из трёх независимых проблем:

### 1. Cookie `Secure=true` по HTTP (корень)

`AuthController.SetAuthCookies` ставил все cookie авторизации (`auth_access_token`, `auth_refresh_token`, `auth_expires_at`) с жёстким `Secure=true`. Браузер **молча отбрасывает** Secure-cookie, полученную по plain HTTP (кроме localhost — trustworthy origin). Результат: cookie вообще не сохранялись, все запросы уходили без токена.

**Почему в dev работало:** `localhost` браузер считает безопасным контекстом и принимает Secure-cookie даже по HTTP. Плюс UI (:5173) и API (:8080) — один хост = same-site.

### 2. EventSource не умеет заголовки

SSE открывается через `EventSource`, который не передаёт `Authorization` — токен может дойти только через cookie. При неработающих cookie SSE умирал сразу и наглядно. Обычные fetch тоже падали (401), но авто-refresh в `httpClient.ts` маскировал часть ошибок — поэтому бросилось в глаза именно SSE.

### 3. У EventSource нет refresh-логики

`SyncView` открывал общий SSE сразу при монтировании, без проверки живости access-токена. Access-cookie короткоживущая (минуты) — SSE не имел механизма продления и умирал насовсем при первом истечении.

**Дополнительно (диагностика):** при открытии UI на `localhost:5173` против API на `10.54.188.90:8080` куки хранятся, но **не отправляются** — `SameSite=Lax` не прикладывается к кросс-сайтовым запросам (разные хосты; порт не важен). Правило: UI должен открываться с того же хоста, что и API.

## Фикс

### Бэкенд

**Файл:** `NsiTransfer/Presentation/Controllers/AuthController.cs` (6 мест: set access/refresh/expiresAt + clear)

```csharp
// Было:
Secure = true,

// Стало:
Secure = Request.IsHttps,
```

Флаг ставится только при реальном HTTPS. По HTTP cookie сохраняются; при переводе на TLS менять ничего не нужно.

**Файл:** `NsiTransfer/Authentication/SimpleBearerAuthenticationHandler.cs`

Третий источник токена: query-параметр `access_token`. Ограничения:
- только `GET`;
- только пути `/api/polynom-sync/listen-for-all-sync-events` и `/api/polynom-sync/listen-for-sync-events` (чтобы токен не попадал в access-логи прокси на прочих эндпоинтах);
- приоритет: cookie → `Authorization: Bearer` → query.

Сейчас фронт query не использует (токен в HttpOnly-cookie, JS недоступен) — задел на будущее.

### Фронтенд

**Файл:** `frontend/nsitransferfront/src/services/polynomSyncService.ts`

Wrapper `listenForAllSyncEvents` вернет `{ close, isUnauthorized }`:
- при обрыве различается сетевой сбой (`eventSource.readyState === CONNECTING` — браузер сам ретраит) от фатального (`CLOSED` — HTTP-ошибка, у нас 401);
- фатальный обрыв → `refreshToken()` → переоткрытие (1 попытка на цикл, счётчик сбрасывается при успешном открытии);
- refresh не удался / попытки исчерпаны → `unauthorized = true`, соединение закрыто, **без** бесконечной молотилки;
- флаги выставляются **до** вызова `onConnectionError` (иначе UI читал бы устаревший `isUnauthorized()`).

**Файл:** `frontend/nsitransferfront/src/views/SyncView.vue`

- Перед открытием общего SSE: `isAccessTokenAlive()`, при мёртвом — `refreshToken()`; не удался → SSE не открывать, показывать баннер `sseUnauthorized`.
- Загрузка списка синхронизаций не блокируется SSE-подпиской (параллельно).
- При `isUnauthorized()` — баннер в UI вместо тихой мёртвой вкладки.

## Как работает при протухании токена

1. **Access истёк, страница загружается заново** → SyncView обновляет токен до открытия SSE.
2. **Access истёк, SSE висит** → сервер рвёт соединение → wrapper делает refresh → переподключается с новой cookie. Цикл повторяется неограниченно.
3. **Refresh истёк (30 дней)** → recovery не удался → баннер «не авторизовано», пользователь перелогинивается.

## Эксплуатация

- **UI открывать с того же хоста, что и API** (например `http://10.54.188.90:5173`, не `localhost:5173`) — иначе Lax-куки не отправляются.
- При будущем TLS-прокси перед Kestrel добавить `UseForwardedHeaders` (X-Forwarded-Proto), иначе `Request.IsHttps` снова соврёт.
- HTTP-деплой = токены в открытом виде в сети — только доверенный сегмент.
- Шум `background.js` / `notifications.bitwarden.com` в консоли — расширение Bitwarden браузера, к проекту отношения не имеет.

## Верификация

1. DevTools → Application → Cookies → `auth_access_token` / `auth_refresh_token` присутствуют.
2. `GET /api/polynom-sync/listen-for-all-sync-events` → 200, SSE держится.
3. `dotnet build` — 0 ошибок; `npm run build` (vue-tsc + vite) — 0 ошибок.

## Сопутствующие материалы

- [[deployment]] — Развёртывание (правило «UI и API на одном хосте»)
- [[environment]] — `VITE_API_BASE_URL` (рантайм-конфиг через entrypoint)
- [[offline-build]] — Пересборка дистрибутива после обновления кода
