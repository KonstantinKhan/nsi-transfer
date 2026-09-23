# Сборка Docker образов под linux/amd64 для offline сервера

**Задача:** Собрать образы под linux/amd64 и экспортировать в tar архив для сервера без интернета.

**Статус:** Вариант 2 (сборка через buildx на Mac/colima) проверен end-to-end — собран,
упакован, перенесён и запущен на реальном Linux-сервере (2026-09-23).

Два варианта сборки — выбирай по машине, которая под рукой:

- **Вариант 1 (ниже):** нативная Linux amd64 машина с интернетом. Просто, без нюансов.
- **Вариант 2:** сборка через `docker buildx` с любой машины — Mac (colima/Docker Desktop) или Windows (Docker Desktop), даже на arm64-хосте. См. раздел [Вариант 2: сборка через buildx](#вариант-2-сборка-через-buildx-mac-windows-любой-хост) ниже.

## Вариант 1: сборка на Linux amd64

**Требования:**
- Linux машина (Ubuntu, Debian, RHEL) С интернетом
- Docker установлен и запущен
- ~2GB свободного места
- Git доступ к репо

## Шаг 1: Клонировать репо

```bash
git clone https://github.com/KonstantinKhan/nsi-transfer.git
cd nsi-transfer
git checkout docker-build-linux
```

## Шаг 2: Собрать образы

```bash
docker compose build
```

**Ожидать:**
- Backend сборка: ~2-5 минут (dotnet restore загружает пакеты)
- Frontend сборка: ~1-2 минуты (npm install)
- Проверить результат:

```bash
docker images | grep nsitransfer
```

Должны быть образы:
- `nsitransfer-nsitransfer:latest` (~104MB)
- `nsitransfer-nsitransferfront:latest` (~27MB)

## Шаг 3: Экспортировать в архив

`docker save` умеет сохранить сразу несколько образов в один tar, а `docker load` сам
распознаёт gzip — отдельный скрипт загрузки не нужен:

```bash
docker save nsitransfer-nsitransfer:latest nsitransfer-nsitransferfront:latest | gzip > nsitransfer-images-amd64.tar.gz
```

**Результат:**
- `nsitransfer-images-amd64.tar.gz` (~130MB)

## Шаг 4: Передать на сервер

```bash
# На твоей машине (macOS/Linux):
scp nsitransfer-images-amd64.tar.gz user@server:/home/user/
```

## На сервере (без интернета)

```bash
# Загрузить оба образа одной командой
docker load -i nsitransfer-images-amd64.tar.gz

# Проверить
docker images | grep nsitransfer

# Запустить сервисы
docker compose up -d
```

## Вариант 2: сборка через buildx (Mac, Windows, любой хост)

Требует, чтобы Dockerfile бэкенда собирал SDK-стадию на `--platform=$BUILDPLATFORM` и кросс-компилировал
dotnet флагом `-a $TARGETARCH` (уже так в `backend/nsitransfer/NsiTransfer/Dockerfile`). Без этого сборка
под чужую архитектуру идёт через QEMU-эмуляцию — `dotnet restore` под эмуляцией виснет или ползёт часами.

**Требования:**
- Docker с buildx-плагином (в Docker Desktop уже есть; на Mac с colima — `brew install docker-buildx`
  и симлинк в `~/.docker/cli-plugins/`)
- На Mac: colima запущен (`colima start`), активный context — `colima` (`docker context use colima`),
  не `default`
- Git доступ к репо

### Шаг 1-2: клонировать репо, собрать образы

```bash
git clone https://github.com/KonstantinKhan/nsi-transfer.git
cd nsi-transfer
git checkout docker-build-linux

docker buildx build --platform linux/amd64 --load \
  -t nsitransfer-nsitransfer:latest \
  -f backend/nsitransfer/NsiTransfer/Dockerfile backend/nsitransfer

docker buildx build --platform linux/amd64 --load \
  -t nsitransfer-nsitransferfront:latest \
  frontend/nsitransferfront
```

**Ожидать:** обе сборки в пределах 1-2 минут (backend SDK-стадия собирается нативно на хосте,
без эмуляции — только целевые слои финального образа тянутся под linux/amd64).

Проверить архитектуру:
```bash
docker image inspect nsitransfer-nsitransfer:latest | grep -i architecture
docker image inspect nsitransfer-nsitransferfront:latest | grep -i architecture
# Обе — "Architecture": "amd64"
```

Дальше — Шаг 3 и Шаг 4 из Варианта 1 (экспорт в архив, передача на сервер) без изменений.

## Troubleshooting

### "docker: command not found"
Убедись что Docker установлен и запущен:
```bash
docker --version
sudo usermod -aG docker $USER
newgrp docker
```

### "Cannot connect to Docker daemon"
Запустить Docker:
```bash
sudo systemctl start docker
# или для Mac/Windows
open /Applications/Docker.app
```

### "Загрузка пакетов зависает"
Проверить интернет:
```bash
ping 8.8.8.8
curl -I https://api.nuget.org
```

### Образы на неправильной платформе
Проверить:
```bash
docker image inspect nsitransfer-nsitransfer:latest | grep -i architecture
# Должно быть: "Architecture": "amd64"
```

### "BuildKit is enabled but the buildx component is missing or broken" (Mac/colima)
Активный docker context не тот, где реально живёт демон (например `default` вместо `colima`),
или buildx-плагин не установлен:
```bash
colima start
docker context use colima
docker buildx ls   # builder "colima" должен быть running
```

### Сборка backend виснет на `dotnet restore`/`dotnet build` под linux/amd64 на Mac/Windows-ARM
Значит SDK-стадия собирается под QEMU-эмуляцией вместо нативной архитектуры хоста — dotnet restore
под эмуляцией практически не двигается. Проверить, что build-стадия в Dockerfile начинается с
`FROM --platform=$BUILDPLATFORM mcr.microsoft.com/dotnet/sdk:8.0 AS build`, а `dotnet build`/`publish`/`restore`
вызываются с флагом `-a $TARGETARCH`. См. [Вариант 2](#вариант-2-сборка-через-buildx-mac-windows-любой-хост).

## Ссылки

- [[deployment]] — развёртывание через Docker Compose, структура docker-compose.yml
- [[offline-build]] — сборка offline-дистрибутива на Windows
- [[configuration]] — конфигурация приложения
