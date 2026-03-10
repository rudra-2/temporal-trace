# Temporal-Trace
Temporal task tracking platform with SQL Server Temporal Tables, EF Core 8, SignalR live sync, and Angular time-travel UI.

## Implementation Docs

- Phased commit plan: `docs/PHASED_COMMIT_PLAN.md`
- Persistent build prompt: `AI_PROMPT.md`

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

## Prerequisites

- Docker Desktop with Linux containers enabled.
- .NET 8 SDK (for local backend development).
- Node 20+ and npm (for local frontend development).

## Smoke Test Expectations

After `./scripts/smoke-test.ps1`, expected outcomes:

- API health endpoint returns `200`.
- Task create and update operations succeed.
- Temporal `as-of` query returns a valid historical snapshot.
- Frontend endpoint returns `200`.

## Troubleshooting

1. Docker engine not reachable
- Open Docker Desktop and wait for `Engine running`.
- Re-run `./scripts/startup.ps1`.

2. SQL container not healthy
- Run `docker compose logs sqlserver`.
- Verify `SA_PASSWORD` in `.env` satisfies SQL Server complexity rules.

3. API fails at startup
- Run `docker compose logs api`.
- Validate DB settings in `.env` and check `API_PORT` availability.

4. Frontend not loading
- Run `docker compose logs frontend`.
- Ensure `FRONTEND_PORT` is not already occupied.

## Local Development Mode

Backend:
- `cd backend/TemporalTrace.Api`
- `dotnet run`

Frontend:
- `cd frontend/temporal-trace-ui`
- `npm start`

Notes:
- Frontend dev server uses `proxy.conf.json` for `/api` and `/hubs` routing.
