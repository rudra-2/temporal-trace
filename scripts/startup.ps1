Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

Set-Location (Join-Path $PSScriptRoot '..')

if (-not (Test-Path '.env')) {
    Copy-Item '.env.example' '.env'
    Write-Host 'Created .env from .env.example. Update secrets if needed.' -ForegroundColor Yellow
}

Write-Host 'Starting Temporal-Trace containers...' -ForegroundColor Cyan
docker compose up --build -d

Write-Host 'Waiting for API health endpoint...' -ForegroundColor Cyan
$attempt = 0
while ($attempt -lt 45) {
    $attempt += 1
    try {
        $result = Invoke-WebRequest -Uri 'http://localhost:5294/healthz' -UseBasicParsing -TimeoutSec 2
        if ($result.StatusCode -eq 200) {
            Write-Host 'API is healthy.' -ForegroundColor Green
            break
        }
    }
    catch {
        Start-Sleep -Seconds 2
    }
}

if ($attempt -ge 45) {
    Write-Error 'API health check timed out. Run: docker compose logs api'
}

Write-Host 'Frontend URL: http://localhost:4200' -ForegroundColor Green
Write-Host 'API URL: http://localhost:5294/swagger' -ForegroundColor Green
