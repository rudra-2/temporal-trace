# Temporal-Trace: Phased Commit Plan

Use this plan to build the project in clean, reviewable commits.
Each phase includes scope, expected output, and a suggested commit message.

## Phase 0 - Repository Setup and Baseline

Goal: Initialize backend and frontend projects with a clear folder structure.

Deliverables:
- `src/backend/TemporalTrace.Api` (.NET 8 Web API)
- `src/frontend/temporal-trace-ui` (Angular 17 app)
- Solution file and basic README refresh

Suggested commit message:
- `chore: initialize Temporal-Trace backend and frontend workspaces`

## Phase 1 - Domain Model and EF Core Temporal Mapping

Goal: Create `ProjectTask` model and configure SQL Server temporal table behavior via EF Core 8.

Deliverables:
- `ProjectTask` entity with fields:
  - `Id`
  - `Title`
  - `Description`
  - `Status`
  - `Priority`
- `AppDbContext` with `DbSet<ProjectTask>`
- `OnModelCreating` configuration with `.ToTable(..., b => b.IsTemporal())`
- Initial migration generated

Suggested commit message:
- `feat(api): add ProjectTask entity and EF Core temporal table mapping`

## Phase 2 - API CRUD and Historical Query Endpoint

Goal: Add task API endpoints including historical state lookup.

Deliverables:
- `TaskController` with CRUD endpoints
- Specialized endpoint:
  - `GET /api/task/{id}/at?targetTime=...`
- Uses `TemporalAsOf(targetTime)` to fetch historical state by id
- Validation for invalid timestamps and not-found task versions

Suggested commit message:
- `feat(api): implement TaskController with temporal as-of endpoint`

## Phase 3 - Real-time Sync with SignalR

Goal: Broadcast live task updates to connected clients.

Deliverables:
- `TemporalHub` SignalR hub
- SignalR registration in `Program.cs`
- Broadcast on create/update/delete:
  - event: `taskUpdated`
  - payload: updated `ProjectTask`
- Endpoint mapping for `/hubs/temporal`

Suggested commit message:
- `feat(api): add SignalR TemporalHub for live task updates`

## Phase 4 - Angular Task Timeline and Slider UX

Goal: Build a time-travel UI driven by slider input.

Deliverables:
- Task list component and service layer
- Time-travel slider range:
  - min: now minus 24 hours
  - max: now
- Slider movement triggers historical API calls
- Use RxJS `switchMap` for canceling stale requests
- If task does not exist at selected time, remove from current view

Suggested commit message:
- `feat(ui): add time-travel slider with temporal task querying`

## Phase 5 - Live + Historical State Coordination

Goal: Merge SignalR live updates with historical mode behavior.

Deliverables:
- SignalR client integration in Angular
- Live mode: immediate updates reflected in task list
- Historical mode: lock view to selected timestamp and ignore live overwrite
- Clear mode indicator in UI (`Live` vs `Time Travel`)

Suggested commit message:
- `feat(ui): sync SignalR updates with live and time-travel modes`

## Phase 6 - Docker Compose Orchestration

Goal: Run all services locally through containers.

Deliverables:
- `docker-compose.yml` with:
  - `sqlserver` (SQL Server 2022)
  - `api` (.NET 8)
  - `frontend` (Angular)
- SQL container env config compatible with temporal tables
- Startup order and health checks where practical
- Connection strings wired through environment variables

Suggested commit message:
- `feat(devops): add docker-compose for api frontend and sqlserver`

## Phase 7 - Future-Proof Features (Incremental)

Goal: Add standout advanced capabilities as optional milestones.

Deliverables (one feature per commit recommended):
- Ghost of Changes Past (side-by-side diff vs current)
- Timeline Branching Simulation (what-if branch)
- Temporal Heatmap (change-frequency analytics)
- Self-Healing Records (restore historical version as new current)

Suggested commit messages:
- `feat(ui): add ghost diff view for historical task comparisons`
- `feat(simulation): add timeline branching what-if analysis`
- `feat(analytics): add temporal heatmap dashboard`
- `feat(api): add self-healing restore from historical version`

## Phase 8 - Hardening, Testing, and Demo Readiness

Goal: Ensure correctness, observability, and presentation quality.

Deliverables:
- Unit and integration tests for temporal querying and hub events
- End-to-end UI checks for slider race conditions
- Seed/demo data scripts
- README with architecture, run steps, and presentation talking points

Suggested commit message:
- `test/docs: finalize quality checks and presentation readiness`

## Recommended Branch and Commit Strategy

- Keep each phase in a separate commit.
- Prefer one concern per commit even inside a phase.
- Tag known checkpoints:
  - `v0.1-foundation`
  - `v0.2-temporal-api`
  - `v0.3-realtime`
  - `v0.4-ui-timetravel`
  - `v0.5-compose`

## Presentation One-Liner (Audit Log Question)

`Temporal-Trace uses native SQL Server system-versioned temporal tables, so history is captured at the database engine level. This is tamper-resistant, queryable with time semantics, and avoids fragile trigger-based audit pipelines.`
