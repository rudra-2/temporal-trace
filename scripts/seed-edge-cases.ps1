Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

Set-Location (Join-Path $PSScriptRoot '..')

$baseUrl = 'http://localhost:5294/api/task'

function Wait-Api {
    Write-Host 'Checking API health before seeding...' -ForegroundColor Cyan
    $attempt = 0
    while ($attempt -lt 45) {
        $attempt += 1
        try {
            $health = Invoke-WebRequest -Uri 'http://localhost:5294/healthz' -UseBasicParsing -TimeoutSec 2
            if ($health.StatusCode -eq 200) {
                Write-Host 'API is healthy.' -ForegroundColor Green
                return
            }
        }
        catch {
            Start-Sleep -Seconds 2
        }
    }

    throw 'API health check failed before seed.'
}

function New-Task {
    param(
        [string]$Title,
        [string]$Description,
        [string]$Status,
        [int]$Priority
    )

    $body = @{
        title = $Title
        description = $Description
        status = $Status
        priority = $Priority
    } | ConvertTo-Json

    return Invoke-RestMethod -Uri $baseUrl -Method Post -ContentType 'application/json' -Body $body
}

function Set-Task {
    param(
        [int]$TaskId,
        [string]$Title,
        [string]$Description,
        [string]$Status,
        [int]$Priority
    )

    $body = @{
        title = $Title
        description = $Description
        status = $Status
        priority = $Priority
    } | ConvertTo-Json

    return Invoke-RestMethod -Uri ("$baseUrl/{0}" -f $TaskId) -Method Put -ContentType 'application/json' -Body $body
}

function Add-WorkUpdate {
    param(
        [int]$TaskId,
        [string]$Note,
        [Nullable[int]]$MinutesSpent = $null,
        [string]$StatusAfter = $null
    )

    $payload = @{
        note = $Note
        statusAfter = $StatusAfter
        minutesSpent = $MinutesSpent
    } | ConvertTo-Json

    return Invoke-RestMethod -Uri ("$baseUrl/{0}/updates" -f $TaskId) -Method Post -ContentType 'application/json' -Body $payload
}

Write-Host 'Seeding edge-case temporal data...' -ForegroundColor Cyan
Wait-Api

# 1) Rapid status flip task
$t1 = New-Task -Title 'Checkout API hardening' -Description 'Flaky errors under peak load' -Status 'Open' -Priority 5
Start-Sleep -Seconds 1
Set-Task -TaskId $t1.id -Title $t1.title -Description 'Initial investigation started' -Status 'InProgress' -Priority 5 | Out-Null
Add-WorkUpdate -TaskId $t1.id -Note 'Reproduced issue with 200 concurrent requests' -MinutesSpent 35 -StatusAfter 'InProgress' | Out-Null
Start-Sleep -Seconds 1
Set-Task -TaskId $t1.id -Title $t1.title -Description 'Dependency timeout discovered' -Status 'Blocked' -Priority 5 | Out-Null
Add-WorkUpdate -TaskId $t1.id -Note 'Blocked by upstream gateway timeout' -MinutesSpent 20 -StatusAfter 'Blocked' | Out-Null
Start-Sleep -Seconds 1
Set-Task -TaskId $t1.id -Title $t1.title -Description 'Patched timeout and retries' -Status 'InProgress' -Priority 4 | Out-Null
Add-WorkUpdate -TaskId $t1.id -Note 'Patch deployed to staging and validated' -MinutesSpent 25 -StatusAfter 'InProgress' | Out-Null

# 2) Done then reopened then done task
$t2 = New-Task -Title 'Onboarding email sequence' -Description 'Automate day-0 to day-7 flow' -Status 'Open' -Priority 3
Start-Sleep -Seconds 1
Set-Task -TaskId $t2.id -Title $t2.title -Description 'Template and trigger complete' -Status 'Done' -Priority 3 | Out-Null
Add-WorkUpdate -TaskId $t2.id -Note 'Initial release completed' -MinutesSpent 40 -StatusAfter 'Done' | Out-Null
Start-Sleep -Seconds 1
Set-Task -TaskId $t2.id -Title $t2.title -Description 'Found rendering issue in Outlook' -Status 'InProgress' -Priority 4 | Out-Null
Add-WorkUpdate -TaskId $t2.id -Note 'Reopened for compatibility fix' -MinutesSpent 30 -StatusAfter 'InProgress' | Out-Null
Start-Sleep -Seconds 1
Set-Task -TaskId $t2.id -Title $t2.title -Description 'Outlook fix verified' -Status 'Done' -Priority 3 | Out-Null
Add-WorkUpdate -TaskId $t2.id -Note 'Final QA passed across major clients' -MinutesSpent 15 -StatusAfter 'Done' | Out-Null

# 3) Long-running in-progress task with multiple updates
$t3 = New-Task -Title 'Usage analytics dashboard v2' -Description 'Drilldowns, cohorts, retention cards' -Status 'InProgress' -Priority 4
Add-WorkUpdate -TaskId $t3.id -Note 'Baseline chart grid implemented' -MinutesSpent 50 -StatusAfter 'InProgress' | Out-Null
Start-Sleep -Seconds 1
Add-WorkUpdate -TaskId $t3.id -Note 'Added retention query optimizations' -MinutesSpent 45 -StatusAfter 'InProgress' | Out-Null
Start-Sleep -Seconds 1
Add-WorkUpdate -TaskId $t3.id -Note 'Need PM sign-off on KPI definitions' -MinutesSpent 15 -StatusAfter 'InProgress' | Out-Null

# 4) Blocked by external dependency
$t4 = New-Task -Title 'SSO integration for enterprise customer' -Description 'SAML + role mapping' -Status 'Blocked' -Priority 5
Add-WorkUpdate -TaskId $t4.id -Note 'Waiting for IdP metadata and certificates from customer IT' -MinutesSpent 20 -StatusAfter 'Blocked' | Out-Null

# 5) Quick-win completed task
$t5 = New-Task -Title 'Landing page copy refresh' -Description 'Improve activation CTA clarity' -Status 'Open' -Priority 2
Start-Sleep -Seconds 1
Set-Task -TaskId $t5.id -Title $t5.title -Description 'Copy update merged and deployed' -Status 'Done' -Priority 2 | Out-Null
Add-WorkUpdate -TaskId $t5.id -Note 'Copy refreshed and published' -MinutesSpent 18 -StatusAfter 'Done' | Out-Null

# 6) Create branch data on a task (for branch scoring + replay)
$targetTime = (Get-Date).ToUniversalTime().AddSeconds(-2).ToString('o')
$branchBodyA = @{ targetTime = $targetTime; branchName = 'WhatIf-FasterShip' } | ConvertTo-Json
$branchBodyB = @{ targetTime = $targetTime; branchName = 'WhatIf-QualityFirst' } | ConvertTo-Json
$branchA = Invoke-RestMethod -Uri ("$baseUrl/{0}/branch" -f $t1.id) -Method Post -ContentType 'application/json' -Body $branchBodyA
$branchB = Invoke-RestMethod -Uri ("$baseUrl/{0}/branch" -f $t1.id) -Method Post -ContentType 'application/json' -Body $branchBodyB

$overrideFast = @{
    overrideTitle = 'Checkout API hardening (fast-track)'
    overrideDescription = 'Ship reduced scope patch today, optimize later'
    overrideStatus = 'InProgress'
    overridePriority = 3
} | ConvertTo-Json
$overrideQuality = @{
    overrideTitle = 'Checkout API hardening (quality-first)'
    overrideDescription = 'Add soak tests and canary safety checks before release'
    overrideStatus = 'Blocked'
    overridePriority = 5
} | ConvertTo-Json

Invoke-RestMethod -Uri ("$baseUrl/branch/{0}/override" -f $branchA.branchId) -Method Put -ContentType 'application/json' -Body $overrideFast | Out-Null
Invoke-RestMethod -Uri ("$baseUrl/branch/{0}/override" -f $branchB.branchId) -Method Put -ContentType 'application/json' -Body $overrideQuality | Out-Null

Write-Host 'Edge-case seed complete.' -ForegroundColor Green
Write-Host ("Created task IDs: {0}, {1}, {2}, {3}, {4}" -f $t1.id, $t2.id, $t3.id, $t4.id, $t5.id) -ForegroundColor Green
Write-Host ("Created branch IDs: {0}, {1}" -f $branchA.branchId, $branchB.branchId) -ForegroundColor Green
