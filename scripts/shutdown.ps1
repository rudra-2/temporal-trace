Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

Set-Location (Join-Path $PSScriptRoot '..')
Write-Host 'Stopping Temporal-Trace containers...' -ForegroundColor Cyan
docker compose down
Write-Host 'Stopped.' -ForegroundColor Green
