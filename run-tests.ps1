#!/usr/bin/env pwsh

# Скрипт для запуска функциональных тестов прокси-сервера

Write-Host "🚀 Запуск функциональных тестов прокси-сервера..." -ForegroundColor Green

# Переходим в корневую директорию
Set-Location $PSScriptRoot

# Собираем проект
Write-Host "📦 Сборка проекта..." -ForegroundColor Yellow
dotnet build

if ($LASTEXITCODE -ne 0) {
    Write-Host "❌ Ошибка сборки!" -ForegroundColor Red
    exit 1
}

# Запускаем тесты
Write-Host "🧪 Запуск тестов..." -ForegroundColor Yellow
dotnet test --logger "console;verbosity=normal"

if ($LASTEXITCODE -eq 0) {
    Write-Host "✅ Все тесты прошли успешно!" -ForegroundColor Green
} else {
    Write-Host "❌ Некоторые тесты не прошли!" -ForegroundColor Red
    exit 1
}
