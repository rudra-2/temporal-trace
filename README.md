# Temporal-Trace
Temporal task tracking platform with SQL Server Temporal Tables, EF Core 8, SignalR live sync, and Angular time-travel UI.

## Implementation Docs

- Phased commit plan: `docs/PHASED_COMMIT_PLAN.md`
- Persistent build prompt: `AI_PROMPT.md`
- Operator runbook: `docs/RUNBOOK.md`

## Quick Start

### 1. Configure environment

- `Copy-Item .env.example .env`

### 2. Start full stack

- `./scripts/startup.ps1`

### 3. Verify services

- Frontend: `http://localhost:4200`
- API Swagger: `http://localhost:5294/swagger`
- API Health: `http://localhost:5294/healthz`

### 4. Smoke test

- `./scripts/smoke-test.ps1`

### 5. Stop stack

- `./scripts/shutdown.ps1`

## Architecture

- `frontend/temporal-trace-ui`: Angular 17 app with time-travel slider and live updates.
- `backend/TemporalTrace.Api`: .NET 8 API with EF Core 8 temporal queries and SignalR hub.
- `docker-compose.yml`: SQL Server 2022 + API + frontend orchestration.

## Core Temporal Endpoints

- `GET /api/task`: current task list.
- `GET /api/task/{id}`: current task by id.
- `GET /api/task/{id}/at?targetTime=...`: historical task snapshot.
- `GET /api/task/at?targetTime=...`: historical snapshot of all tasks.
