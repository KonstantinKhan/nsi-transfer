Write-Host "--- Развертывание NsiTransfer ---" -ForegroundColor Green

Write-Host "Загрузка Docker-образа из 'nsitransfer_image.tar'..."
docker load -i "nsitransfer_image.tar"
if ($LASTEXITCODE -ne 0) {
    Write-Host "Ошибка загрузки Docker-образа. Операция прервана." -ForegroundColor Red
    exit 1
}
Write-Host "Образ успешно загружен."

Write-Host "Запуск сервисов командой 'docker-compose up -d'..."
docker-compose up -d
if ($LASTEXITCODE -ne 0) {
    Write-Host "Ошибка запуска сервисов. Проверьте логи командой 'docker-compose logs'." -ForegroundColor Red
    exit 1
}

Write-Host "--- NsiTransfer успешно запущен! ---" -ForegroundColor Green
Write-Host "Приложение доступно по адресу http://localhost:8080"