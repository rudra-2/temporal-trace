Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

Write-Host 'Running Temporal-Trace smoke test...' -ForegroundColor Cyan

$health = Invoke-WebRequest -Uri 'http://localhost:5294/healthz' -UseBasicParsing
if ($health.StatusCode -ne 200) {
    throw 'API health check failed.'
}

$now = (Get-Date).ToUniversalTime().ToString('o')
$created = Invoke-RestMethod -Uri 'http://localhost:5294/api/task' -Method Post -ContentType 'application/json' -Body '{"title":"Smoke Task","description":"startup smoke","status":"Open","priority":3}'
Start-Sleep -Milliseconds 1000
$updated = Invoke-RestMethod -Uri ("http://localhost:5294/api/task/{0}" -f $created.id) -Method Put -ContentType 'application/json' -Body '{"title":"Smoke Task","description":"updated","status":"InProgress","priority":2}'
$asOf = Invoke-RestMethod -Uri ("http://localhost:5294/api/task/{0}/at?targetTime={1}" -f $created.id, ((Get-Date).ToUniversalTime().ToString('o'))) -Method Get
$ui = Invoke-WebRequest -Uri 'http://localhost:4200' -UseBasicParsing

Write-Host "Created ID: $($created.id)" -ForegroundColor Green
Write-Host "Updated Status: $($updated.status)" -ForegroundColor Green
Write-Host "Temporal As-Of Status: $($asOf.status)" -ForegroundColor Green
Write-Host "Frontend HTTP: $($ui.StatusCode)" -ForegroundColor Green
