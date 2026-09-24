# Развёртывание через Docker Compose

Как поднять бэкенд + фронтенд в Docker на новой машине, подключиться к удалённым сервисам
(Polynom, PostgreSQL, RabbitMQ, SMTP) и проверить, что всё работает. Переменные окружения
подробно расписаны в [[environment]] — здесь только специфика самого развёртывания.

---

## 1. Требования

| Что | Версия | Проверка |
|---|---|---|
| Docker Engine | ≥ 24 | `docker -v` |
| Docker Compose plugin | v2+ | `docker compose version` |
| Свободные порты на хосте | 8080 (бэк), 5173 (фронт) | `ss -ltn \| grep -E '8080\|5173'` |
| Сетевой доступ до Polynom API | HTTP/HTTPS (обычно 80/443) | `curl http://<polynom-address>` |
| Сетевой доступ до PostgreSQL | 5432 | `nc -zv <pg-host> 5432` |
| Сетевой доступ до RabbitMQ | 5672 | `nc -zv <rabbit-host> 5672` |
| Сетевой доступ до SMTP | 587/465 | `nc -zv <smtp-host> 587` |

Windows: Docker Desktop + WSL2. Файлы проекта держать в файловой системе WSL (не `/mnt/c/...`) —
сборка на порядок быстрее.

## 2. Архитектура

Два контейнера, общий compose в `docker-compose.yml`:

| Контейнер | Образ | Порт хоста | Назначение |
|---|---|---|---|
| `nsitransfer_server` | .NET 8 (`backend/nsitransfer/NsiTransfer/Dockerfile`) | 8080 | REST API `/api/*` |
| `nsitransferfront_web` | nginx + Vue 3 bundle (`frontend/nsitransferfront/Dockerfile`) | 5173 | UI |

```
nsitransfer/
├── docker-compose.yml          # общий compose (бэк + фронт)
├── .env.example                # шаблон переменных → скопировать в .env
├── backend/nsitransfer/
│   ├── NsiTransfer/Dockerfile  # образ бэка (.NET 8)
│   └── config/                 # рабочая конфигурация (директория монтируется в контейнер целиком)
└── frontend/nsitransferfront/
    ├── Dockerfile              # образ фронта (node → nginx)
    └── nginx.conf              # отдача SPA
```

Внешние зависимости (НЕ в compose, подключаются через `.env`):
- **PostgreSQL** — обязательный; при старте бэк сам гонит миграции (`MigrateDbContext`). Нет БД → контейнер падает в рестарт-луп.
- **Polynom API** — обязательный; логин/синхронизация идут через него.
- **RabbitMQ** — нужен для очередей синхронизации.
- **SMTP** — только для email-уведомлений об ошибках; логину не мешает.

Ключевой факт: **фронт ходит в API напрямую из браузера** (не через nginx-прокси). Адрес
(`VITE_API_BASE_URL`) читается контейнером **в рантайме** — `docker-entrypoint.sh` генерирует
`/config.json` из переменной окружения при старте, фронт фетчит его перед загрузкой приложения.
Смена адреса — просто пересоздание контейнера, пересборка образа не нужна (подробности — [[environment]]).

## 3. Запуск — по шагам

1. Установить Docker Desktop (Windows) / Docker Engine + compose plugin (Linux). Windows: включить
   WSL2 integration для дистрибутива, если работа из WSL.
2. Скопировать проект целиком (нужны `backend/`, `frontend/`, `docker-compose.yml`, `.env.example`).
3. `cp .env.example .env`, заполнить (см. [[environment]]).
4. Если внешние сервисы стоят **на этой же машине** (локальный Polynom/Postgres/Rabbit):
   - в `.env` адрес = `host.docker.internal`, НЕ `127.0.0.1`/`localhost`;
   - открыть порты в файрволе (см. п.5) — иначе из контейнера будет таймаут;
   - сервисы должны слушать на `0.0.0.0`, не только на `127.0.0.1` (Windows-проверка:
     `Get-NetTCPConnection -State Listen`).
5. Если внешние сервисы удалённые — нужен сетевой доступ/VPN до них с машины Docker.
6. Собрать и поднять:
   ```bash
   docker compose up -d --build
   ```
7. Проверка (см. п.6 ниже).

Смена значений в `.env`:
- любая переменная бэка → `docker compose up -d` (пересоздаст контейнер; **`restart` НЕ перечитывает `.env`!**);
- `VITE_API_BASE_URL` → `docker compose up -d --force-recreate nsitransferfront` — тоже без пересборки образа.

## 4. Windows Firewall — главный источник таймаутов

Симптом: контейнер → `host.docker.internal:<порт>` таймаут, при этом с самой Windows
`Invoke-WebRequest http://127.0.0.1:<порт>` работает.

Причина: входящие коннекты из docker-сети (vEthernet) режутся файрволом. TCP-коннект есть только
там, где правило уже создано (например, Postgres часто его имеет).

Фикс (PowerShell от администратора), подставить реальные порты локальных сервисов:

```powershell
New-NetFirewallRule -DisplayName "Polynom 5100 for Docker" -Direction Inbound -Protocol TCP -LocalPort 5100 -Action Allow
New-NetFirewallRule -DisplayName "RabbitMQ 5672 for Docker" -Direction Inbound -Protocol TCP -LocalPort 5672 -Action Allow
```

Рестарт контейнеров не нужен.

## 4.1 Права на bind-mount каталоги (config-data, logs)

Симптом: сохранение конфига через UI падает `Access to the path '/app/config-data/configuration.json.tmp' is denied`, при этом сам `configuration.json` читается нормально.

Механика:

1. Бэк в контейнере работает **не от root**: в `Dockerfile` — `USER $APP_UID`. В официальных .NET-образах
   (`mcr.microsoft.com/dotnet/aspnet:8.0`) это юзер `app` с uid/gid **1654** (`ENV APP_UID=1654`, константа образа).
2. Bind mount **пробрасывает владельца и права хоста в контейнер как есть** — «внутри докера» и «на хосте»
   это одни и те же файлы.
3. Сохранение конфига атомарно (`JsonConfigurationWriter.WriteAtomicAsync`): создаётся **новый** файл
   `configuration.json.tmp`, затем `File.Move` поверх основного. Обе операции требуют **w на каталог**
   у uid 1654 — права на сам файл (`chmod 666`) не решают ничего.
4. Если каталога на хосте нет, Docker создаёт его сам с владельцем **root** → приложение писать не может.

Фикс (на хосте, где docker):

```bash
mkdir -p backend/config backend/logs        # каталоги создать ДО `up`
cp backend/nsitransfer/config/configuration.json backend/config/   # при первом запуске
sudo chown -R 1654:1654 backend/config backend/logs
docker compose up -d --build
```

Важные свойства:

- `chown` именно (смена владельца), не `chmod 777` (дыра для всего хоста)
- владелец хост-каталога переживает `down/up`, `--build`, пересоздание контейнера — фикс одноразовый
- `chmod`/`chown` через `docker exec` **не персистентны** для каталогов из слоя контейнера и сбиваются
  при пересоздании; для bind mount они меняют хост-файлы, но делать это надо сразу на хосте
- проверить uid процесса: `docker exec nsitransfer_server id` → `uid=1654(app) gid=1654(app)`

## 5. Проверка работы

1. **Контейнеры подняты:**
   ```bash
   docker compose ps
   # оба сервиса status = Up (у фронта — healthy)
   ```
2. **Бэк стартовал без ошибок конфигурации:**
   ```bash
   docker compose logs nsitransfer | grep -E 'слушает|error|Error|Invalid'
   ```
   Ожидаемо: `Приложение слушает на: http://+:8080`. Если переменные в `.env` неполные —
   контейнер упадёт при старте: `Параметр '...' обязателен` — дозаполнить `.env` и `docker compose up -d`.
3. **Фронт отвечает:**
   ```bash
   curl -I http://localhost:5173
   # HTTP/1.1 200 OK
   ```
4. **API доступен:**
   ```bash
   curl -i http://localhost:8080/api/auth/storages
   # любой ответ отличный от connection refused — API жив (401/404/4xx ок)
   ```
5. **Сквозной тест через UI:**
   - Открыть `http://localhost:5173` (с другой машины — `http://<IP>:5173`)
   - Войти (логин/пароль `PolynomAuthCreds`) — логин уходит в бэк → удалённый Polynom
   - Запустить синхронизацию из UI, убедиться, что отправки появляются в списке

## 6. Диагностика — быстрый разбор

| Симптом | Причина | Что смотреть / решение |
|---|---|---|
| Контейнер бэка падает на старте | Неполный `.env` (валидация `ValidateOnStart`) | `docker compose logs nsitransfer`, дозаполнить `.env` |
| «Failed to fetch» в UI | Бэк-контейнер упал (обычно БД) | `docker compose ps`, `docker compose logs nsitransfer` |
| Рестарт-луп бэка + `NpgsqlException Failed to connect` | БД недоступна / неверный Host | строка подключения; для локальной БД — `host.docker.internal` |
| `Параметр '...' обязателен` при старте | Неполный `.env` | текст ошибки называет точное поле |
| `The '<host>' scheme is not supported` | В `PolynomConfig__Address` нет `http://` | добавить схему |
| `Connection refused <ip>:80` | В Address нет порта → стучится на 80 | указать реальный порт |
| Запрос висит 100 сек → `HttpClient.Timeout` | TCP есть, порт слушается, но файрвол/сервис не отвечает; или неверный порт | см. п.4; проверить порт сервиса |
| Логин не проходит, ошибки сети | Нет доступа до Polynom/Postgres/RabbitMQ с машины Docker | проверить `curl`/`nc` из п.1, прокси/файрвол |
| Фронт открылся, но API-запросы падают / логин ок с одной машины, не работает с другой | `VITE_API_BASE_URL=localhost` вместо IP машины | поставить IP в `.env`, `docker compose up -d --force-recreate nsitransferfront` (без ребилда) |
| `port is already allocated` | Заняты 8080/5173 | сменить `BACKEND_PORT`/`FRONTEND_PORT` |
| Сохранение конфига через UI → `Device or resource busy: /app/...json` | Атомарная запись (`File.Move`) поверх single-file bind mount невозможна | монтировать директорию + `CONFIG_FILE_PATH` (уже сделано в compose) |
| Сохранение конфига через UI → `Access to the path '/app/config-data/configuration.json.tmp' is denied` | У uid 1654 нет **записи на каталог** `backend/config` на хосте (создание tmp + rename требуют w на каталог; владелец — root или юзер хоста) | `sudo chown -R 1654:1654 backend/config backend/logs` на хосте (см. п.4.1) |
| `Не удалось опубликовать отправление ... в брокер сообщений` при живом порте 5672 | TCP есть, но auth/vhost/права падают. Чаще всего `guest` с не-localhost запрещён (loopback_users) | создать юзера: `rabbitmqctl add_user nsitransfer <pass>` + `set_permissions -p / nsitransfer ".*" ".*" ".*"`; на Windows CLI — полный путь `...\rabbitmq_server-3.12.8\sbin\rabbitmqctl.bat`, при ошибке node — выровнять Erlang cookie (`systemprofile\.erlang.cookie` → профиль юзера) или Management UI `localhost:15672` |
| SMTP: `SslHandshakeException ... unexpected EOF` из контейнера | Режим TLS/порт не совпадают с сервером; либо relay режет STARTTLS из docker-подсетей | проба из docker-сети: `docker run --rm curlimages/curl -v smtp://<host>:<port>`; рабочий вариант для smtp.ascon.ru: `Port=25`, `UseSsl=false` (587 обрывает STARTTLS из контейнера, 465 недоступен из docker-сети) |

Полезные команды:
```bash
docker compose logs nsitransfer --since 10m | grep -A15 ERR     # стек ошибки по времени из ответа API
docker compose ps                                                # статус
docker compose logs nsitransfer | grep слушает                   # бэк поднялся: "Приложение слушает на ..."
```
В ответе API об ошибке есть `details` с точным временем — по нему исключение находится в логах
(`grep -B2 -A20 "07:44:01"`).

## 7. Остановка

```bash
docker compose down            # остановить
docker compose down --rmi all  # остановить + удалить образы (код обновился → пересборка)
```

## 8. История фронтенд/деплой-фиксов

- `src/config/apiConfig.ts` — изначально `BASE_URL` из `import.meta.env.VITE_API_BASE_URL`
  (build-time, fallback `http://localhost:8080/api`); позже переведён на рантайм-конфиг через
  `window.__APP_CONFIG__` / `docker-entrypoint.sh` (2026-09-22, см. [[environment]]).
- `Dockerfile` фронта: `node:24-alpine` → `npm install` → `build` → `nginx:alpine`, плюс `nginx.conf`
  (SPA-fallback) и `.dockerignore`.
- Healthcheck фронта: Alpine резолвит `localhost` → IPv6 `::1`, nginx слушает IPv4 → в HEALTHCHECK
  нужен `http://127.0.0.1/`.
- vue-tsc сборка падала — исправлено: `<script setup>` без `lang="ts"` в 4 файлах (`MainView`,
  `FooterBar`, `BackButton`, `ConfigurationView`); добавлен отсутствующий тип `GetSendingsParams`
  (`sync.types.ts`, импорт в `polynomSyncService.ts`); типизированы props/параметры в `FooterBar.vue`,
  `ConfigurationView.vue`, `MainView.vue`.
- Сохранение конфигурации через UI в Docker падало `Device or resource busy` — исправлено: путь к
  `configuration.json` настраивается env `CONFIG_FILE_PATH` (`WebApplicationBuilderExtensions`,
  `JsonConfigurationWriter`), compose монтирует директорию `config` → `/app/config-data` вместо
  одиночного файла.
- Сохранение конфигурации через UI в Docker падало `Access to the path ... .tmp is denied` —
  у uid 1654 (`USER $APP_UID`, юзер `app` из .NET-образа) не было w на bind-mount каталог
  `backend/config` (создание `configuration.json.tmp` + `File.Move` требуют записи на каталог).
  Фикс: `sudo chown -R 1654:1654 backend/config backend/logs` на хосте (2026-09-24, см. п.4.1).
- Backend `Dockerfile`: SDK-стадия собирается нативно (`--platform=$BUILDPLATFORM`) с
  кросс-компиляцией под целевую архитектуру (`-a $TARGETARCH`) — иначе сборка `linux/amd64` на
  arm64-хосте (Mac/colima) виснет на `dotnet restore` под QEMU-эмуляцией (2026-09-22). Подробности
  сборки под конкретные платформы — [[docker-build-linux-amd64]].

## 9. Чек-лист передачи на новую машину

- [ ] `.env` заполнен реальными значениями (не `CHANGE_ME`)
- [ ] Реальный порт локального Polynom уточнён и вписан в `PolynomConfig__Address` со схемой `http://`
- [ ] Порты локальных сервисов открыты в файрволе (п.4)
- [ ] `VITE_API_BASE_URL` соответствует способу захода (localhost vs IP машины); при смене — `docker compose up -d --force-recreate nsitransferfront`, без ребилда
- [ ] Пароль SMTP, засвеченный в переписке, сменён
- [ ] Для удалённого сервера — проверить VPN-доступ с новой машины: `Test-NetConnection <host> -Port <порт>`

## Связанные страницы

- [[environment]] — полный справочник переменных окружения
- [[configuration]] — конфигурация приложения
- [[docker-build-linux-amd64]] — сборка образов под linux/amd64 (Linux-хост или cross-build через buildx)
- [[offline-build]] — сборка офлайн-дистрибутива на Windows
