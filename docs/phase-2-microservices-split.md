# Phase 2 — Microservices split, Docker Compose, API Gateway

## Status: done, verified end-to-end

All 5 projects build, `dotnet format` is clean, both services' EF Core migrations have no drift, and all 11 xUnit tests pass across repeated runs. `docker compose up --build` was run for real (Docker Desktop wasn't running at first - had to be started mid-phase) and the full flow was exercised through the gateway with curl:

- `POST /events` → `GET /events/{id}` (both correctly routed to Events)
- `POST /events/{id}/seats` (seat map generation, correctly routed to Seats even though it shares the `/events/{id}` prefix)
- `POST .../reserve` → conflict on a second reserve → `POST .../book` → `GET .../seats?status=Booked` (all correctly routed to Seats)
- `GET /events` still resolves to Events afterward, proving the `Order`-based route precedence works in both directions, not just for the specific case tested first
- Confirmed via `psql -l` that `events` and `seats` exist as two genuinely separate databases on the one Postgres instance

Both services' direct Swagger UIs (`:5101/swagger`, `:5102/swagger`) were also confirmed reachable. CI's `docker-build` job (builds all three Dockerfiles on GitHub's runners) stays in place as an ongoing regression check.

## What got built

The Phase 1 monolith (`TicketFlow.Api`) split into three services:

```
                    ┌──────────────┐
   client ───────▶  │   Gateway    │  (YARP reverse proxy)
                    └──────┬───────┘
                 ┌─────────┴─────────┐
                 ▼                   ▼
         ┌───────────────┐   ┌───────────────┐
         │  Events API   │   │  Seats API    │
         └───────┬───────┘   └───────┬───────┘
                 ▼                   ▼
         ┌───────────────┐   ┌───────────────┐
         │  events (db)  │   │  seats (db)   │
         └───────────────┘   └───────────────┘
              (one postgres instance, two databases)
```

- **`TicketFlow.Events.Api`** — owns `Event` only. `CreateEventRequest`/`EventResponse` lost the seat-map/seat-count fields they had in Phase 1.
- **`TicketFlow.Seats.Api`** (new) — owns `Seat`, including the seat-map generation endpoint (moved here from Events — seats are its domain now). `EventId` on `Seat` is a plain `Guid`, not a foreign key: it points at a row in a *different service's database*, and relational FKs can't cross that boundary. The service doesn't verify the event exists before generating seats for it — it just trusts the caller. That's a real gap, and it's deliberately left open as the motivation for Phase 3's gRPC call.
- **`TicketFlow.Gateway`** (new) — a YARP reverse proxy. The single address a client talks to; it routes to whichever service actually owns the requested data.

## What's auto-generated vs. hand-written (new in this phase)

Same pattern as Phase 1 — `dotnet new webapi`/`dotnet new web` scaffolded each project skeleton (`.csproj`, `Properties/launchSettings.json`, `appsettings*.json` base, `Program.cs` boilerplate that got fully rewritten), `dotnet add package` edited the `.csproj` package list, and `dotnet ef migrations add` generated the `Migrations/` folders. Everything under `Models/`, `Data/`, `Contracts/`, `Endpoints/`, the rewritten `Program.cs` files, both `TicketFlowXApiFactory.cs` test factories, all test files, all three `Dockerfile`s, `docker-compose.yml`, and `db/init/01-create-databases.sh` are hand-written.

## The API Gateway (YARP) — the actual "routing middleware" piece

`TicketFlow.Gateway/Program.cs` is almost nothing:
```csharp
builder.Services.AddReverseProxy().LoadFromConfig(builder.Configuration.GetSection("ReverseProxy"));
var app = builder.Build();
app.MapReverseProxy();
```
All the actual routing logic lives in config (`appsettings.json`), not code:
```json
"Routes": {
  "seats-route":  { "ClusterId": "seats-cluster",  "Order": 0,  "Match": { "Path": "/events/{eventId}/seats/{**catch-all}" } },
  "events-route": { "ClusterId": "events-cluster", "Order": 10, "Match": { "Path": "/events/{**catch-all}" } }
}
```
`Order: 0` on the seats route matters — without it, a request to `/events/abc/seats/xyz` could match the more general `/events/**` pattern first and get routed to Events instead of Seats. Lower `Order` = checked first = higher priority.

**Environment-specific destinations**: the base `appsettings.json` points `Clusters` at Docker Compose service names (`http://events-api:8080`, `http://seats-api:8080`) — those hostnames only resolve inside the Compose network. `appsettings.Development.json` overrides just the `Address` values to `http://localhost:5101`/`http://localhost:5102`, so running the gateway with a plain `dotnet run` outside Docker still works, pointing at services also run locally. This is the standard ASP.NET Core layered-config pattern (JSON config merges by key path, so overriding one nested `Address` string doesn't require repeating the whole `ReverseProxy` section).

## Two EF Core gotchas hit while splitting the tests

Both were hit for the same underlying reason: Phase 1's tests swapped SQLite-for-SQLite (same provider, different connection). Phase 2's services use Postgres in production, so the test factories now swap *providers* (Postgres → SQLite), which turned out to be a meaningfully different problem.

**1. "Only a single database provider can be registered."** Copying Phase 1's factory pattern (remove the `DbContextOptions<T>` descriptor, add a new `AddDbContext` call with `UseSqlite`) threw this exception at test startup. Root cause: `UseNpgsql()`/`UseSqlite()` each register their own EF Core provider services as a *side effect* of being called — onto the app's `IServiceCollection` directly, independent of the specific `DbContextOptions<T>` descriptor. Removing that one descriptor doesn't undo the side effect; once `Program.cs`'s `UseNpgsql()` call has run, its provider services are registered regardless of what the test factory does afterward. Fix: **never call `UseNpgsql()` at all in the test environment**, rather than trying to undo it. Each service's `Program.cs` now guards the real provider registration:
```csharp
if (!builder.Environment.IsEnvironment("Testing"))
{
    options.UseNpgsql(builder.Configuration.GetConnectionString(...));
}
```
and each test factory calls `builder.UseEnvironment("Testing")` (a custom environment name, deliberately not `"Development"`, so it also doesn't accidentally trigger the app's Swagger/auto-migrate startup block).

**2. Postgres-typed migrations don't necessarily apply to SQLite.** The generated migrations hardcode Postgres-native SQL type names directly in the C# migration code — `type: "uuid"`, `type: "text"`, `type: "timestamp with time zone"`. Running `db.Database.MigrateAsync()` against a SQLite connection with these migrations is relying on SQLite's very permissive type-affinity system to paper over type names it doesn't recognize — not something to depend on. Fix: test factories build the schema with `db.Database.EnsureCreated()` instead, via `protected override IHost CreateHost(IHostBuilder builder)`. `EnsureCreated()` builds tables straight from the current EF Core model, sidestepping the migration files (and their provider-specific SQL) entirely. Tests don't need migration history — they need a schema that matches the current code, which is exactly what `EnsureCreated()` gives them.

Both fixes are captured in `TicketFlowEventsApiFactory.cs`/`TicketFlowSeatsApiFactory.cs` and in each service's `Program.cs`, with comments pointing back to this reasoning.

## Docker

Each service has a **multi-stage Dockerfile**: an SDK image builds and publishes the app, then a much smaller ASP.NET runtime image just runs the published output — the SDK (compiler, NuGet caches, etc.) never ships in the final image. The `.csproj` is copied and restored in its own layer *before* the rest of the source is copied — Docker caches each layer, so a source-only change (no package reference changes) reuses the cached `dotnet restore` layer on rebuild instead of redownloading every NuGet package.

All three Dockerfiles use **repo root as the build context** (`context: .` in `docker-compose.yml`), not each project's own folder — that's what lets `COPY src/TicketFlow.Events.Api/...` paths work, and it's the standard shape for a multi-project monorepo where a Dockerfile needs to be scoped to one project without the compose file needing per-project build contexts.

`docker-compose.yml` runs one `postgres:17-alpine` container with **two databases on one instance** (`events`, `seats`), created by `db/init/01-create-databases.sh` (the Postgres image runs any script in `/docker-entrypoint-initdb.d/` once, only on first startup with an empty data volume — `POSTGRES_DB` only lets you name *one* default database, hence the extra script for the second). This is a deliberate compromise: true "database per service" in a real deployment usually means separate database *instances* too, but running two full Postgres containers for a local learning project is unnecessary overhead. What matters — each service only ever connects to its own database and never queries the other's — is preserved either way.

Events and Seats both run with `ASPNETCORE_ENVIRONMENT=Development` inside Compose (unusual for containers, deliberate here) — this is a demo project, and keeping Swagger UI and the auto-migrate-on-boot logic active in the "containerized" run makes it something you can actually open and click through, not just a black box. The gateway runs as `Production` (the default if unset — spelled out explicitly in the compose file), since it has no Development-only behavior to gain from it.

## How to run

```bash
docker compose up --build
```
Then:
- Gateway (the one address a client needs): `http://localhost:5100`
- Events service directly + Swagger: `http://localhost:5101/swagger`
- Seats service directly + Swagger: `http://localhost:5102/swagger`

Without Docker, run Postgres alone and each service with `dotnet run` (uses the ports pinned in each `launchSettings.json`: Gateway 5100, Events 5101, Seats 5102):
```bash
docker compose up postgres
cd src/TicketFlow.Events.Api && dotnet run    # separate terminals
cd src/TicketFlow.Seats.Api && dotnet run
cd src/TicketFlow.Gateway && dotnet run
```

## Decisions and why

- **One Postgres instance, two databases** rather than two Postgres containers — see the Docker section above.
- **`Order` set explicitly on both YARP routes** rather than relying on implicit route-specificity ordering — explicit is safer and easier to reason about than trusting the router to prefer the "more specific" pattern on its own.
- **Test environment named `"Testing"`, not reused `"Development"`** — keeps test-host behavior from silently depending on whatever the Development-gated startup block happens to do, and made the Program.cs provider-selection guard read cleanly as "only touch the real database outside of tests."
- **`docker-build` kept in CI even after local verification** — it's independent signal (GitHub's runners have their own Docker daemon) that catches a Dockerfile regression on every push, not just when someone happens to run Compose locally.
