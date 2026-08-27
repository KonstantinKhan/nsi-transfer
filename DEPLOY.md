# NsiTransfer — запуск через Docker Compose

Инструкция: поднять бэкенд + фронтенд в Docker на любой машине, подключиться к удалённому серверу (Polynom, PostgreSQL, RabbitMQ, SMTP) и проверить работу.

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

Windows: Docker Desktop + WSL2. Файлы проекта рекомендуется держать в файловой системе WSL (не `/mnt/c/...`) — сборка на порядок быстрее.

---

## 2. Структура

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

---

## 3. Переменные окружения (.env)

```bash
cp .env.example .env
```

Формат имён: `Секция__Поле` (двойное подчёркивание) — стандарт ASP.NET Core.

### 3.1 Подключение к удалённому серверу

| Переменная | Что означает | Где взять значение |
|---|---|---|
| `ConnectionStrings__DefaultConnection` | Строка подключения к PostgreSQL | Администратор БД. Формат: `Host=<host>;Port=5432;Database=nsi_transfer;Username=<user>;Password=<pass>;Include Error Detail=true` |
| `PolynomConfig__Address` | Базовый URL сервера Polynom | Адрес удалённого сервера, без слэша в конце. Пример: `http://172.23.14.181` |
| `PolynomConfig__DbName` | Имя БД Polynom | Администратор Polynom |
| `PolynomConfig__TimeZoneId` | Часовой пояс сервера Polynom | Обычно `Russian Standard Time` |
| `PolynomAuthCreds__Username` / `__Password` | Логин/пароль учётки Polynom API | Администратор Polynom |
| `RabbitMqCreds__Host` / `__Port` / `__Username` / `__Password` / `__VirtualHost` | Доступ к RabbitMQ | Администратор RabbitMQ. `VirtualHost` чаще `/` |
| `SmtpSettings__Server` / `__Port` / `__SenderName` / `__SenderEmail` / `__Username` / `__Password` / `__UseSsl` | Отправка email-уведомлений об ошибках | Администратор почты |

### 3.2 Фронт

| Переменная | Что означает |
|---|---|
| `VITE_API_BASE_URL` | Адрес API бэка, **как его видит браузер пользователя**. Запекается в бандл при сборке образа — после смены требуется пересборка (см. ниже). |

Правило выбора значения:

- Заходите на фронт с той же машины, где Docker: `http://localhost:8080/api`
- Заходите с других компов: `http://<IP-или-имя-машины-с-Docker>:8080/api` (узнать IP: `ip a` / `ipconfig`)

### 3.3 Прочее

| Переменная | По умолчанию | Что означает |
|---|---|---|
| `ASPNETCORE_ENVIRONMENT` | `Production` | `Development` включает Swagger: `http://localhost:8080/swagger` |
| `BACKEND_PORT` / `FRONTEND_PORT` | `8080` / `5173` | Порты на хосте, если заняты — сменить |

### 3.4 Статическая конфигурация

Файл `backend/nsitransfer/config/configuration.json` доступен контейнеру через монтирование **директории** `config` → `/app/config-data` (путь задаётся переменной `CONFIG_FILE_PATH`). Монтировать директорию, а не одиночный файл, обязательно: сохранение конфигурации через UI использует атомарную замену файла (`File.Move`), которая поверх single-file bind mount падает с `Device or resource busy`. Редактируется на хосте, применяется рестартом `docker compose restart nsitransfer`. Часть полей меняется на лету через UI (`/api/configuration`) — эти изменения видны и в файле на хосте.

---

## 4. Сборка и запуск

```bash
cd nsitransfer
cp .env.example .env   # если ещё не сделано, затем заполнить

docker compose up -d --build
```

Пересборка только фронта после смены `VITE_API_BASE_URL`:

```bash
docker compose build nsitransferfront
docker compose up -d nsitransferfront
```

---

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
   Ожидаемо: `Приложение слушает на: http://+:8080`.
   Если переменные в `.env` неполные — контейнер упадёт при старте: в логах будет сообщение вида
   `Параметр '...' обязателен` — дозаполнить `.env` и `docker compose up -d`.

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
   - Войти (логин/пароль `PolynomAuthCreds`) — логин уходит в бэк → удалённый Polynom. Успешный вход = связь с удалённым сервером есть
   - Запустить синхронизацию из UI, убедиться, что отправки появляются в списке

6. **Типовые проблемы:**
   | Симптом | Причина | Решение |
   |---|---|---|
   | Контейнер бэка падает на старте | Неполный `.env` (валидация `ValidateOnStart`) | Читать `docker compose logs nsitransfer`, дозаполнить `.env` |
   | Логин не проходит, ошибки сети | Нет доступа до Polynom/Postgres/RabbitMQ с машины Docker | Проверить `curl`/`nc` из п.1, прокси/файрвол |
   | Фронт открылся, но API-запросы падают | Неверный `VITE_API_BASE_URL` (браузер не достучался) | Установить адрес с IP машины, пересобрать фронт |
   | `port is already allocated` | Заняты 8080/5173 | Сменить `BACKEND_PORT`/`FRONTEND_PORT` |

---

## 6. Остановка

```bash
docker compose down          # остановить
docker compose down --rmi all  # остановить + удалить образы (код обновился → пересборка)
```
