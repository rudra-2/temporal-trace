# AI Prompt for Temporal-Trace

Use this prompt whenever generating or reviewing code for this repository.

## System Intent

You are building `Temporal-Trace`, a high-performance .NET 8 Web API + Angular 17 system using SQL Server temporal tables through EF Core 8.

Primary goals:
- Historical task state retrieval by timestamp
- Real-time synchronization with SignalR
- Time-travel UI with race-safe reactive API calls
- Containerized local environment with SQL Server 2022

## Non-Negotiable Technical Requirements

1. Backend stack:
- .NET 8 Web API
- EF Core 8 with SQL Server provider
- `ProjectTask` entity fields:
  - `Id`
  - `Title`
  - `Description`
  - `Status`
  - `Priority`

2. Temporal configuration:
- Configure `ProjectTask` as a system-versioned temporal table in `OnModelCreating`.
- Must use EF Core temporal mapping (`.IsTemporal()`).

3. Historical endpoint:
- In `TaskController`, provide:
  - `GetTaskAtTime(int id, DateTime targetTime)`
- Must query with `TemporalAsOf(targetTime)`.

4. Real-time backend:
- Integrate SignalR hub named `TemporalHub`.
- On task updates, broadcast updated task payload to connected clients.

5. Frontend behavior:
- Angular component with time-travel slider.
- Slider range: from `24 hours ago` to `Now`.
- On slider movement, call historical API and update UI.
- If a task did not exist at selected time, remove from displayed list.
- Must use RxJS `switchMap` to prevent race conditions during rapid slider movement.

6. Docker orchestration:
- Create `docker-compose.yml` for:
  - Angular frontend
  - .NET API
  - SQL Server 2022
- SQL container must be configured properly for temporal table support.

## Architecture Guidance

- Use clean layering:
  - Controllers for transport concerns
  - Services for business logic (optional but preferred)
  - DbContext for persistence
- Keep DTOs separate from EF entities where practical.
- Ensure UTC handling is explicit for all historical queries.
- Expose clear API routes, for example:
  - `GET /api/task/{id}/at?targetTime=...`

## Quality Gates

Before finalizing any change, verify:
- Temporal migrations apply successfully.
- `TemporalAsOf` endpoint returns correct historical records.
- SignalR events are emitted on update paths.
- Frontend slider cancels stale requests (`switchMap`).
- Docker services start and interconnect correctly.

## Future Scope Features to Mention or Implement

1. Ghost of Changes Past:
- Side-by-side diff of historical vs current task data.

2. Timeline Branching Simulation:
- Branch from historical state for what-if projections.

3. Temporal Heatmap:
- Visualize high-change periods by hour/day.

4. Self-Healing Records:
- Restore a historical version as a new current version.

## Presentation Talking Point

If asked "Why not a normal audit log table?":

`Temporal-Trace uses native SQL Server system-versioning, so history is maintained by the database engine itself. This provides stronger integrity, cleaner time-based querying, and better performance than manual trigger or app-layer audit logging.`

## Commit Discipline Prompt

When implementing, follow phased commits:
- One phase at a time
- One concern per commit
- Include short test evidence in commit body when possible
- Keep commit messages semantic (`feat`, `fix`, `docs`, `test`, `chore`)

Reference file:
- `docs/PHASED_COMMIT_PLAN.md`
