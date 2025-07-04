#!/usr/bin/env pwsh

# Create HTTPS certificate for ProxyServer
# Usage: .\create-certificate.ps1 [password]

param(
    [string]$Password = "ProxyServerDev2025"
)

Write-Host "Creating HTTPS certificate for ProxyServer..." -ForegroundColor Green

# Function to read HTTPS port from settings.json
function Get-HttpsPortFromSettings {
    $settingsPath = "./ProxyServer/settings.json"
    if (Test-Path $settingsPath) {
        try {
            $settings = Get-Content $settingsPath | ConvertFrom-Json
            return $settings.HttpsPort
        } catch {
            Write-Host "Warning: Could not read HttpsPort from settings.json, using default 8043" -ForegroundColor Yellow
            return 8043
        }
    } else {
        Write-Host "Warning: settings.json not found, using default HTTPS port 8043" -ForegroundColor Yellow
        return 8043
    }
}

# Create certificates directory if it doesn't exist
$certificatesDir = "./ProxyServer/certificates"
if (!(Test-Path $certificatesDir)) {
    New-Item -ItemType Directory -Path $certificatesDir -Force | Out-Null
    Write-Host "Created certificates directory: $certificatesDir" -ForegroundColor Yellow
}

# Generate certificate
$certPath = "$certificatesDir/proxy.pfx"
Write-Host "Generating certificate: $certPath" -ForegroundColor Cyan

try {
    dotnet dev-certs https -ep $certPath -p $Password --trust
    
    if ($LASTEXITCODE -eq 0) {
        $httpsPort = Get-HttpsPortFromSettings
        Write-Host "Certificate created successfully!" -ForegroundColor Green
        Write-Host "Certificate path: $certPath" -ForegroundColor Yellow
        Write-Host "Certificate password: $Password" -ForegroundColor Yellow
        Write-Host ""
        Write-Host "Next steps:" -ForegroundColor Cyan
        Write-Host "1. Update settings.json with the certificate path and password" -ForegroundColor White
        Write-Host "2. Run the proxy server: dotnet run" -ForegroundColor White
        Write-Host "3. Access via HTTPS: https://localhost:$httpsPort" -ForegroundColor White
    } else {
        Write-Host "Failed to create certificate. Exit code: $LASTEXITCODE" -ForegroundColor Red
        exit 1
    }
} catch {
    Write-Host "Error creating certificate: $($_.Exception.Message)" -ForegroundColor Red
    exit 1
}
