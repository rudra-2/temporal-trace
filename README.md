# Temporal-Trace
Temporal task tracking platform with SQL Server Temporal Tables, EF Core 8, SignalR live sync, and Angular time-travel UI.

## About

**Temporal-Trace** is a full-stack project management tool that lets you travel back in time through your task history. Unlike conventional audit-log approaches, it uses **SQL Server system-versioned temporal tables** so that every change to a task is captured automatically at the database engine level — no application-layer triggers or manual history tables required.

### What problem does it solve?

Standard task trackers only show the *current* state of work. Temporal-Trace gives you a complete, queryable history of every task so you can answer questions like:

- "What did task #42 look like three days ago?"
- "Which tasks existed at the beginning of last sprint?"
- "How did this task's status change over time?"

### Key Features

| Feature | Description |
|---|---|
| **Time-travel queries** | Query any task's exact state at any past timestamp via `GET /api/task/{id}/at?targetTime=…` |
| **Historical snapshot list** | Retrieve the full task list as it existed at a given moment via `GET /api/task/at?targetTime=…` |
| **Side-by-side diff** | Compare historical vs. current task fields with `GET /api/task/{id}/compare?targetTime=…` |
| **Timeline branching** | Fork a task from a historical point to explore "what-if" projections without altering the main timeline |
| **Live sync** | SignalR (`TemporalHub`) broadcasts every create, update, and delete so connected clients update in real time |
| **Time-travel slider UI** | Angular 17 slider ranging from *24 hours ago* to *Now*; uses RxJS `switchMap` to prevent stale-request races |
| **Live vs. Time-Travel mode** | UI clearly indicates whether you are viewing live data or a historical snapshot |

### Why temporal tables instead of an audit log?

> Temporal-Trace uses native SQL Server system-versioned temporal tables, so history is maintained by the database engine itself. This provides stronger integrity, cleaner time-based querying, and better performance than manual trigger or app-layer audit logging.

### Tech Stack

| Layer | Technology |
|---|---|
| Frontend | Angular 17, RxJS, SignalR JS client |
| Backend | .NET 8 Web API, EF Core 8 |
| Database | SQL Server 2022 (system-versioned temporal tables) |
| Real-time | ASP.NET Core SignalR |
| Container | Docker Compose |

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
