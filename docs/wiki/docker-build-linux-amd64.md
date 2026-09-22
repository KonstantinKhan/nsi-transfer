# Сборка Docker образов под linux/amd64 для offline сервера

**Задача:** Собрать образы под linux/amd64 и экспортировать в tar архив для сервера без интернета.

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
docker-compose build
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

## Шаг 3: Экспортировать в tar

```bash
# Создать директорию для архива
mkdir -p docker-images
cd docker-images

# Сохранить образы
docker save nsitransfer-nsitransfer:latest > backend.tar
docker save nsitransfer-nsitransferfront:latest > frontend.tar

# Создать скрипт загрузки
cat > load.sh << 'EOF'
#!/bin/bash
echo "Загрузка Docker образов..."
docker load -i backend.tar
docker load -i frontend.tar
echo "✓ Образы загружены успешно"
echo ""
docker images | grep nsitransfer
EOF
chmod +x load.sh

# Упаковать архив
cd ..
tar -czf nsitransfer-docker-linux-amd64.tar.gz docker-images/
```

**Результат:**
- `nsitransfer-docker-linux-amd64.tar.gz` (~130MB)

## Шаг 4: Передать на сервер

```bash
# На твоей машине (macOS):
scp nsitransfer-docker-linux-amd64.tar.gz user@server:/home/user/
```

## На сервере (без интернета)

```bash
# Распаковать архив
tar -xzf nsitransfer-docker-linux-amd64.tar.gz
cd docker-images

# Загрузить образы
./load.sh

# Проверить
docker images | grep nsitransfer

# Запустить сервисы
cd ../
docker-compose up -d
```

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

## Ссылки

- [[offline-build]] — сборка offline-дистрибутива на Windows
- [[configuration]] — конфигурация приложения
- [[docker-compose.yml]] — конфигурация контейнеров
