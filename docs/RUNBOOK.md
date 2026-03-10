# Temporal-Trace Runbook

## Prerequisites

- Docker Desktop with Linux containers enabled
- .NET 8 SDK (for local backend dev)
- Node 20+ and npm (for local frontend dev)

## Startup (Containerized)

1. Copy env file:
- `Copy-Item .env.example .env`

2. Start stack:
- `./scripts/startup.ps1`

3. Verify:
- Frontend: `http://localhost:4200`
- API Swagger: `http://localhost:5294/swagger`
- API Health: `http://localhost:5294/healthz`

## Smoke Test

- `./scripts/smoke-test.ps1`

Expected:
- API health returns `200`
- Task create/update succeed
- Temporal `as-of` query returns task snapshot
- Frontend returns `200`

## Shutdown

- `./scripts/shutdown.ps1`

## Troubleshooting

1. Docker engine not reachable:
- Open Docker Desktop and wait until it reports `Engine running`
- Re-run `./scripts/startup.ps1`

2. SQL container not healthy:
- `docker compose logs sqlserver`
- Confirm `SA_PASSWORD` in `.env` satisfies SQL Server complexity

3. API fails on startup:
- `docker compose logs api`
- Check DB connection fields in `.env` and free port for `API_PORT`

4. Frontend not loading:
- `docker compose logs frontend`
- Ensure port `FRONTEND_PORT` is not occupied

## Local Dev Mode (Without Docker)

Backend:
- `cd backend/TemporalTrace.Api`
- `dotnet run`

Frontend:
- `cd frontend/temporal-trace-ui`
- `npm start`

Notes:
- Frontend dev server uses `proxy.conf.json` for `/api` and `/hubs`.
