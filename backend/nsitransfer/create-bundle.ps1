[CmdletBinding()]
param (
    [Parameter(Mandatory = $false)]
    [switch]$NoCache
)

# Этот скрипт создает самодостаточный пакет (бандл) для развертывания приложения NsiTransfer.

# --- Конфигурация ---
$ImageName = "nsitransfer" # Должно совпадать с именем сервиса в docker-compose.yml
$ImageTag = "latest"
$FullImageName = "${ImageName}:${ImageTag}"
$BaseBundleDir = "nsitransfer-bundle"
$ImageArchiveName = "nsitransfer_image.tar"

# --- Начало скрипта ---
Write-Host "--- Создание бандла для NsiTransfer ---" -ForegroundColor Green

# 1. Определение уникального имени для папки бандла
$BundleDir = $BaseBundleDir
if (Test-Path $BundleDir) {
    $i = 1
    while ($true) {
        $nextDir = "$BaseBundleDir ($i)"
        if (-not (Test-Path $nextDir)) {
            $BundleDir = $nextDir
            break
        }
        $i++
    }
}
Write-Host "Итоговая папка для бандла: $BundleDir" -ForegroundColor Cyan

# 2. Сборка Docker-образа с помощью docker-compose
Write-Host "Сборка Docker-образа '$FullImageName'..."
if ($NoCache) {
    Write-Host "Выполняется сборка без использования кеша (--no-cache)." -ForegroundColor Yellow
    docker-compose build --no-cache $ImageName
}
else {
    docker-compose build $ImageName
}

if ($LASTEXITCODE -ne 0) {
    Write-Host "Ошибка сборки Docker-образа. Операция прервана." -ForegroundColor Red
    exit 1
}
Write-Host "Docker-образ успешно собран." -ForegroundColor Green

# 3. Создание структуры папок для бандла
Write-Host "Создание папки бандла: $BundleDir"
New-Item -ItemType Directory -Path $BundleDir | Out-Null
New-Item -ItemType Directory -Path "$BundleDir/config" | Out-Null

# 4. Сохранение Docker-образа в .tar архив
Write-Host "Сохранение образа '$FullImageName' в архив '$($BundleDir)/$ImageArchiveName'..."
docker save -o "$BundleDir/$ImageArchiveName" $FullImageName
if ($LASTEXITCODE -ne 0) {
    Write-Host "Ошибка сохранения Docker-образа. Операция прервана." -ForegroundColor Red
    exit 1
}
Write-Host "Образ успешно сохранен." -ForegroundColor Green

# 5. Копирование необходимых конфигурационных файлов
Write-Host "Копирование конфигурационных файлов..."
Copy-Item -Path ".env" -Destination "$BundleDir/"
Copy-Item -Path "docker-compose.yml" -Destination "$BundleDir/"
Copy-Item -Path "config/configuration.json" -Destination "$BundleDir/config/"
Copy-Item -Path "run.ps1" -Destination "$BundleDir/"
Copy-Item -Path "bundle.README.md" -Destination "$BundleDir/README.md"
Write-Host "Файлы скопированы." -ForegroundColor Green

Write-Host ""
Write-Host "--- Бандл успешно создан в папке '$BundleDir'! ---" -ForegroundColor Cyan
Write-Host "Теперь вы можете заархивировать папку '$BundleDir' и перенести ее на другую машину."