# Temporal-Trace Project Map (Tree + Quick Purpose)

Read this like a map, not a paragraph.
Format: `name  -> why it exists`

```text
temporal-trace/
|-- .config/                              -> local/editor config metadata
|-- .env                                  -> real environment values (ports, DB creds, names)
|-- .env.example                          -> template for .env setup
|-- .gitignore                            -> prevents generated/local files entering git
|-- docker-compose.yml                    -> runs SQL + API + Frontend together
|-- LICENSE                               -> legal usage terms
|-- README.md                             -> quick start, architecture, commands
|-- PROJECT_STRUCTURE.md                  -> this visual structure guide
|
|-- backend/                              -> server-side app (ASP.NET Core + EF Core)
|   `-- TemporalTrace.Api/
|       |-- Program.cs                    -> app startup: services, CORS, Swagger, SignalR, migrations
|       |-- appsettings.json              -> base runtime config
|       |-- appsettings.Development.json  -> local dev config overrides
|       |-- TemporalTrace.Api.csproj      -> .NET project dependencies/build config
|       |-- Dockerfile                    -> backend container build recipe
|       |
|       |-- Controllers/
|       |   `-- TaskController.cs         -> main API surface (tasks, history, branches, replay, scoring, standup)
|       |
|       |-- Contracts/                    -> API request/response DTOs (transport models)
|       |   |-- ProjectTaskUpsertRequest.cs      -> create/update input model
|       |   |-- ProjectTaskResponse.cs           -> task output model
|       |   |-- ProjectTaskComparisonResponse.cs -> historical vs current diff output
|       |   |-- TaskBranchContracts.cs           -> branch create/read/override/timeline models
|       |   |-- TaskWorkUpdateContracts.cs       -> work log request/response models
|       |   `-- TaskIntelligenceContracts.cs     -> replay/scoring/standup models
|       |
|       |-- Data/
|       |   `-- AppDbContext.cs           -> EF table mapping + temporal table config
|       |
|       |-- Hubs/
|       |   `-- TemporalHub.cs            -> SignalR real-time channel (task updates/deletes)
|       |
|       |-- Models/                       -> DB entities (stored business data)
|       |   |-- ProjectTask.cs            -> core task entity
|       |   |-- TaskBranch.cs             -> what-if branch entity + override fields
|       |   `-- TaskWorkUpdate.cs         -> task progress log entries
|       |
|       `-- Migrations/                   -> schema history (versioned DB changes)
|           |-- 20260310050723_InitialTemporalProjectTask.cs
|           |-- 20260310094040_AddTaskBranches.cs
|           |-- 20260310173646_AddTaskBranchOverrides.cs
|           |-- 20260314160139_AddTaskLifecycleAndWorkUpdates.cs
|           `-- AppDbContextModelSnapshot.cs
|
|-- frontend/                             -> Angular UI (dashboard + interaction layer)
|   `-- temporal-trace-ui/
|       |-- package.json                  -> frontend dependencies/scripts (start/build/test)
|       |-- angular.json                  -> Angular build/workspace config
|       |-- proxy.conf.json               -> dev proxy for /api and /hubs
|       |-- Dockerfile                    -> frontend container build recipe
|       |-- nginx.conf                    -> serves built UI in container
|       |
|       `-- src/
|           |-- main.ts                   -> Angular app bootstrap entry
|           |-- index.html                -> browser host page
|           |-- styles.scss               -> global styles
|           |
|           `-- app/
|               |-- app.component.ts      -> main UI logic (state, forms, filters, API calls)
|               |-- app.component.html    -> main dashboard layout
|               |-- app.component.scss    -> dashboard styling
|               |-- app.config.ts         -> Angular providers/config
|               |-- app.routes.ts         -> route definitions
|               |
|               |-- models/               -> TypeScript interfaces for frontend typing
|               |   |-- project-task.ts
|               |   |-- task-branch.ts
|               |   |-- task-comparison.ts
|               |   |-- task-intelligence.ts
|               |   `-- task-work-update.ts
|               |
|               `-- services/
|                   |-- task-api.service.ts      -> all REST calls to backend
|                   `-- temporal-hub.service.ts  -> SignalR client wiring
|
`-- scripts/                              -> run/ops/demo helpers
    |-- startup.ps1                       -> starts stack and waits for health
    |-- shutdown.ps1                      -> stops stack
    |-- smoke-test.ps1                    -> quick end-to-end sanity checks
    `-- seed-edge-cases.ps1               -> seeds demo-ready temporal edge-case data
```

## 30-Second Architecture Story

- Backend is the brain: API logic + SQL temporal history + real-time events.
- Frontend is the cockpit: manage tasks, view history, branches, replay, scoring, standup.
- Scripts are the operator tools: start, stop, verify, and seed demo data fast.
- Docker Compose is the environment glue: one command runs everything consistently.
