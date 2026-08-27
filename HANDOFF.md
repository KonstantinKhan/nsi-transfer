# NsiTransfer — передача инженеру: знания сессии и инструкция по запуску

Дополняет `DEPLOY.md` (базовая инструкция). Здесь — практические выводы, подводные камни и чек-лист запуска на новой машине, полученные в ходе реального развёртывания и отладки.

---

## 1. Что это и как устроено

Два контейнера, общий compose в `nsitransfer/docker-compose.yml`:

| Контейнер | Образ | Порт хоста | Назначение |
|---|---|---|---|
| `nsitransfer_server` | .NET 8 (`backend/nsitransfer/NsiTransfer/Dockerfile`) | 8080 | REST API `/api/*` |
| `nsitransferfront_web` | nginx + Vue 3 bundle (`frontend/nsitransferfront/Dockerfile`) | 5173 | UI |

Внешние зависимости (НЕ в compose, подключаются через `.env`):
- **PostgreSQL** — обязательный; при старте бэк сам гонит миграции (`MigrateDbContext`). Нет БД → контейнер падает в рестарт-луп.
- **Polynom API** — обязательный; логин/синхронизация идут через него.
- **RabbitMQ** — нужен для очередей синхронизации.
- **SMTP** — только для email-уведомлений об ошибках; логину не мешает.

Ключевой факт: **фронт ходит в API напрямую из браузера** (не через nginx-прокси). Значение `VITE_API_BASE_URL` запекается в JS-бандл при сборке образа.

## 2. Запуск на новой машине — по шагам

1. Установить Docker Desktop (Windows) / Docker Engine + compose plugin (Linux). Windows: включить WSL2 integration для дистрибутива, если работа из WSL.
2. Скопировать проект целиком (нужны `backend/`, `frontend/`, `docker-compose.yml`, `.env.example`).
3. `cp .env.example .env`, заполнить (см. п.3).
4. Если внешние сервисы стоят НА ЭТОЙ ЖЕ машине (локальный Polynom/Postgres/Rabbit):
   - в `.env` адрес = `host.docker.internal`, НЕ `127.0.0.1`/`localhost`;
   - открыть порты в Windows Firewall (см. п.4) — иначе из контейнера будет таймаут;
   - сервисы должны слушать на `0.0.0.0`, не только на `127.0.0.1` (проверка: `Get-NetTCPConnection -State Listen`).
5. Если внешние сервисы удалённые — нужен сетевой доступ/VPN до них с машины Docker.
6. `docker compose up -d --build`
7. Проверка: `docker compose ps` (оба Up, фронт healthy) → `http://localhost:5173` → логин.

## 3. Переменные `.env` — проверенные форматы

Формат имён: `Секция__Поле`. Важны СХЕМА и ПОРТ в адресах — обе ошибки ниже были в реальности:

```bash
# Polynom — ОБЯЗАТЕЛЬНО со схемой http:// и портом.
# "host.docker.internal:49001" без http:// → runtime-ошибка "The 'host.docker.internal' scheme is not supported"
# Голый хост без порта → пойдёт на :80 → connection refused/таймаут
PolynomConfig__Address=http://host.docker.internal:5100        # локальный (реальный порт уточнить!)
# PolynomConfig__Address=http://172.23.14.181                  # удалённый (VPN!)
PolynomConfig__DbName=polynom
PolynomConfig__TimeZoneId=Russian Standard Time
PolynomAuthCreds__Username=...
PolynomAuthCreds__Password=...

# PostgreSQL
# Внутри контейнера 127.0.0.1 = сам контейнер. Хост-машина = host.docker.internal.
ConnectionStrings__DefaultConnection=Host=host.docker.internal;Port=5432;Database=nsi_transfer;Username=postgres;Password=...;Include Error Detail=true

# RabbitMQ
RabbitMqCreds__Host=host.docker.internal
RabbitMqCreds__Port=5672
RabbitMqCreds__Username=guest
RabbitMqCreds__Password=guest
RabbitMqCreds__VirtualHost=/

# SMTP (некорректный хост не ломает логин, только email-уведомления)
SmtpSettings__Server=smtp.example.ru
SmtpSettings__Port=587
SmtpSettings__SenderName=NsiTransfer
SmtpSettings__SenderEmail=...
SmtpSettings__Username=...
SmtpSettings__Password=...
SmtpSettings__UseSsl=true

# Адрес API, как его видит БРАУЗЕР пользователя:
#   заходят с той же машины:      http://localhost:8080/api
#   заходят с других компов:      http://<IP-машины-с-Docker>:8080/api
VITE_API_BASE_URL=http://localhost:8080/api

ASPNETCORE_ENVIRONMENT=Production   # Development включает Swagger на :8080/swagger
BACKEND_PORT=8080
FRONTEND_PORT=5173
```

Смена значений:
- поменял `.env` → `docker compose up -d` (пересоздаст контейнер; `restart` НЕ перечитывает `.env`!);
- поменял `VITE_API_BASE_URL` → пересборка образа: `docker compose build nsitransferfront && docker compose up -d nsitransferfront`.

## 4. Windows Firewall — главный источник таймаутов

Симптом: контейнер → `host.docker.internal:<порт>` таймаут, при этом с самой Windows `Invoke-WebRequest http://127.0.0.1:<порт>` работает.

Причина: входящие коннекты из docker-сети (vEthernet) режутся файрволом. TCP-коннект есть только там, где правило уже создано (например, Postgres часто его имеет).

Фикс (PowerShell от администратора), подставить реальные порты локальных сервисов:

```powershell
New-NetFirewallRule -DisplayName "Polynom 5100 for Docker" -Direction Inbound -Protocol TCP -LocalPort 5100 -Action Allow
New-NetFirewallRule -DisplayName "RabbitMQ 5672 for Docker" -Direction Inbound -Protocol TCP -LocalPort 5672 -Action Allow
```

Рестарт контейнеров не нужен.

## 5. Диагностика — быстрый разбор

| Симптом | Причина | Что смотреть |
|---|---|---|
| «Failed to fetch» в UI | Бэк-контейнер упал (обычно БД) | `docker compose ps`, `docker compose logs nsitransfer` |
| Рестарт-луп бэка + `NpgsqlException Failed to connect` | БД недоступна / неверный Host | строка подключения; для локальной БД — `host.docker.internal` |
| `Параметр '...' обязателен` при старте | Неполный `.env` (валидация `ValidateOnStart`) | текст ошибки называет точное поле |
| `The '<host>' scheme is not supported` | В `PolynomConfig__Address` нет `http://` | добавить схему |
| `Connection refused <ip>:80` | В Address нет порта → стучится на 80 | указать реальный порт |
| Запрос висит 100 сек → `HttpClient.Timeout` | TCP есть, порт слушается, но файрвол/сервис не отвечает; или неверный порт | п.4; проверить порт сервиса |
| Сохранение конфига через UI → `Device or resource busy: /app/...json` | Атомарная запись (`File.Move`) поверх single-file bind mount невозможна | монтировать директорию + `CONFIG_FILE_PATH` (уже сделано в compose) |
| `Не удалось опубликовать отправление ... в брокер сообщений` при живом порте 5672 | TCP есть, но auth/vhost/права падают. Чаще всего `guest` с не-localhost запрещён (loopback_users) | создать юзера: `rabbitmqctl add_user nsitransfer <pass>` + `set_permissions -p / nsitransfer ".*" ".*" ".*"`; на Windows CLI — полный путь `...\rabbitmq_server-3.12.8\sbin\rabbitmqctl.bat`, при ошибке node — выровнять Erlang cookie (`systemprofile\.erlang.cookie` → профиль юзера) или Management UI `localhost:15672` |
| SMTP: `SslHandshakeException ... unexpected EOF` из контейнера | Режим TLS/порт не совпадают с сервером; либо relay режет STARTTLS из docker-подсетей | проба из docker-сети: `docker run --rm curlimages/curl -v smtp://<host>:<port>`; рабочий вариант для smtp.ascon.ru: `Port=25`, `UseSsl=false` (587 обрывает STARTTLS из контейнера, 465 недоступен из docker-сети) |
| Логин ок с машины, не работает с другого компа | `VITE_API_BASE_URL=localhost` | поставить IP машины, пересобрать фронт |

Полезные команды:
```bash
docker compose logs nsitransfer --since 10m | grep -A15 ERR     # стек ошибки по времени из ответа API
docker compose ps                                                # статус
docker compose logs nsitransfer | grep слушает                   # бэк поднялся: "Приложение слушает на ..."
```
В ответе API об ошибке есть `details` с точным временем — по нему исключение находится в логах (`grep -B2 -A20 "07:44:01"`).

## 6. Фиксы, внесённые в код в этой сессии

- `src/config/apiConfig.ts` — `BASE_URL` из `VITE_API_BASE_URL` (fallback `http://localhost:8080/api`); тип в `env.d.ts`.
- `Dockerfile` фронта (node:24-alpine → npm ci → build → nginx:alpine) + `nginx.conf` (SPA-fallback) + `.dockerignore`.
- Healthcheck фронта: Alpine резолвит `localhost` → IPv6 `::1`, nginx слушает IPv4 → в HEALTHCHECK `http://127.0.0.1/`.
- vue-tsc сборка падала — исправлено: `<script setup>` без `lang="ts"` в 4 файлах (`MainView`, `FooterBar`, `BackButton`, `ConfigurationView`); добавлен отсутствующий тип `GetSendingsParams` (`sync.types.ts`, импорт в `polynomSyncService.ts`); типизированы props/параметры в `FooterBar.vue`, `ConfigurationView.vue`, `MainView.vue`.
- Сохранение конфигурации через UI в Docker падало `Device or resource busy` — исправлено: путь к `configuration.json` настраивается env `CONFIG_FILE_PATH` (`WebApplicationBuilderExtensions`, `JsonConfigurationWriter`), compose монтирует директорию `config` → `/app/config-data` вместо одиночного файла.

## 7. Чек-лист передачи

- [ ] `.env` заполнен реальными значениями (не `CHANGE_ME`)
- [ ] Реальный порт локального Polynom уточнён и вписан в `PolynomConfig__Address` со схемой `http://`
- [ ] Порты локальных сервисов открыты в файрволе (п.4)
- [ ] `VITE_API_BASE_URL` соответствует способу захода (localhost vs IP машины); при смене — пересборка фронта
- [ ] Пароль SMTP, засвеченный в переписке, сменён
- [ ] Для удалённого сервера 172.23.14.181 — проверить VPN-доступ с новой машины: `Test-NetConnection 172.23.14.181 -Port <порт>`
