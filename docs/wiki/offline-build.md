# Сборка офлайн-дистрибутива (Windows)

Инструкция для сборки `dist/` после обновления кода — когда нужно передать новый архив на сервер без интернета. Пошагово, без пропусков.

## 0. Проверить, что Docker Desktop реально запущен

Частая ошибка: `docker compose build` падает с
```
failed to connect to the docker API at npipe:////./pipe/dockerDesktopLinuxEngine
```
Значит Docker Desktop не запущен (или ещё не поднялся). Открыть Docker Desktop (иконка кита), подождать статус "Engine running" внизу окна. Проверить командой:

```powershell
docker info
```

Если ошибка — ждать/перезапускать Docker Desktop, дальше не идти. Если данные без ошибок — можно продолжать.

## 1. Перейти в КОРЕНЬ репозитория

**Важно:** не в `dist/`! В `dist/docker-compose.yml` нет `build:` — только `image:` (готовые образы). Сборка внутри `dist/` ничего не пересоберёт, `docker save` потом схватит старые образы из кеша Docker.

```powershell
cd C:\MyProjects\ascon\nsitransfer
```

## 2. Собрать образы

```powershell
docker compose build --no-cache
```

`--no-cache` — обязательно, иначе Docker может переиспользовать старый слой и в образ не попадут изменения (проверено на практике — без флага сборка "прошла", но образ остался старым).

## 3. Проверить, что образ реально новый

```powershell
docker images | Select-String nsitransfer
```

Смотреть колонку `CREATED` — должна быть "сегодня"/"минуты назад", не старая дата. Если дата старая — сборка не сработала, повторить шаг 2, проверить что стоишь в корне (шаг 1).

## 4. Сохранить образы в архив

```powershell
docker save nsitransfer-nsitransfer:latest nsitransfer-nsitransferfront:latest -o dist\nsitransfer-images.tar.gz
```

Перезаписывает `dist\nsitransfer-images.tar.gz`. Проверить дату и размер файла:

```powershell
Get-Item dist\nsitransfer-images.tar.gz
```

Свежая `LastWriteTime`, размер похожий на прошлый (сумма образов, обычно 300-500MB).

## 5. Собрать итоговый архив для передачи

```powershell
cd dist
Compress-Archive -Path * -DestinationPath ..\nsitransfer-offline-$(Get-Date -Format yyyyMMdd).zip -Force
```

Результат — `nsitransfer-offline-<дата>.zip` в корне репо. Это и есть архив для сервера без интернета.

## Состав dist/ и что в нём менять руками

Menять нужно **только** когда меняется код/конфигурация — сам `dist/docker-compose.yml`, `.env.example`, `config/configuration.json` синхронизировать с корневыми файлами вручную (`docker compose` их не трогает):

```powershell
Copy-Item docker-compose.yml dist\docker-compose.yml   # ЕСЛИ менялся — но dist-версия использует image: вместо build:, автослиянием нельзя, вручную сверить разницу
Copy-Item .env.example dist\.env.example
Copy-Item backend\nsitransfer\config\configuration.json dist\config\configuration.json
Copy-Item HANDOFF.md dist\HANDOFF.md
Copy-Item DEPLOY.md dist\DEPLOY.md
```

Если менялся `docker-compose.yml` в корне — **не копировать напрямую**, там разная структура (`build:` vs `image:`). Сверить diff и перенести только реальные изменения (новые переменные окружения, порты, volumes).

## Что НЕ нужно делать

- Не запускать `docker compose build` внутри `dist/` — там нечего собирать.
- Не пропускать шаг 3 (проверка даты образа) — единственный способ поймать "тихий" промах сборки до того, как потратишь время на упаковку архива.

## Связанные страницы

- [[configuration]] — конфигурация приложения
- [[environment]] — переменные окружения
