# Docker образы для запуска на сервере

## Содержимое архива

- `backend.tar` - NsiTransfer backend (104MB)
- `frontend.tar` - NsiTransfer frontend (27MB)
- `load.sh` - скрипт загрузки образов

## Использование

### На Linux сервере (с интернетом)

Если есть интернет, проще пересобрать:

```bash
git clone <repo>
cd nsi-transfer
docker-compose build
docker-compose up
```

### На Linux сервере (без интернета)

1. Распаковать архив:
```bash
tar -xzf nsitransfer-docker.tar.gz
cd docker-images  # или текущая директория
```

2. Загрузить образы:
```bash
./load.sh
```

3. Запустить сервисы:
```bash
docker-compose up
```

## Ограничения

Образы собраны на macOS. Если на сервере проблемы с платформой - пересобирайте локально.
