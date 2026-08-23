# Phase 1 — Single API: Events + Seats

## What got built

A single ASP.NET Core Web API (deliberate monolith, no microservices yet) covering the **Events** and **Seats** domain: create an event with a generated seat map, list events/seats, and move a seat through a hold-then-confirm booking flow — `Available → Held → Booked`, or `Held → Available` if the hold is released. Backed by EF Core + SQLite for local dev. Covered by an xUnit integration test suite and gated by GitHub Actions CI.

## Project structure — what's auto-generated vs. hand-written

| File | Origin |
|---|---|
| `TicketFlow.sln` | Auto — `dotnet new sln` |
| `src/TicketFlow.Api/TicketFlow.Api.csproj` | Auto skeleton (`dotnet new webapi`) + auto-edited by `dotnet add package`; only `<TargetFramework>` hand-edited |
| `src/TicketFlow.Api/Properties/launchSettings.json` | Auto, untouched |
| `src/TicketFlow.Api/appsettings.json` | Auto skeleton, hand-added the `ConnectionStrings` block |
| `src/TicketFlow.Api/appsettings.Development.json` | Auto, untouched |
| `src/TicketFlow.Api/TicketFlow.Api.http` | Auto, untouched |
| `.config/dotnet-tools.json` | Auto — `dotnet new tool-manifest` + `dotnet tool install dotnet-ef` |
| `src/TicketFlow.Api/Migrations/*.cs` | Auto — `dotnet ef migrations add`, generated from the model classes |
| `src/TicketFlow.Api/Models/`, `Data/`, `Contracts/`, `Endpoints/` | 100% hand-written |
| `src/TicketFlow.Api/Program.cs` | Started as the auto weather-forecast template, fully rewritten |
| `tests/TicketFlow.Api.Tests/*.csproj` | Auto skeleton (`dotnet new xunit`) + auto-edited by `dotnet add package`/`dotnet add reference` |
| `tests/TicketFlow.Api.Tests/*.cs` | 100% hand-written |
| `.github/workflows/ci.yaml` | 100% hand-written |

## Domain model

```
Event
  Id, Name, VenueName, StartsAtUtc, Description
  Seats: List<Seat>

Seat
  Id, EventId, Section, Row, Number
  Status: Available | Held | Booked
  HeldUntilUtc (set only while Held)
```

```
Available --reserve--> Held --book--> Booked
             Held --release--> Available
```

A hold lasts 10 minutes (`SeatEndpoints.HoldDuration`). An expired hold can't be booked — `book` checks `HeldUntilUtc > now`. Nothing currently *sweeps* expired holds back to `Available` automatically; that's expected to arrive with Redis TTLs in Phase 5.

The unique constraint `(EventId, Section, Row, Number)` is enforced at the database level (`TicketFlowDbContext.OnModelCreating`), not just in application code — two layers of protection against duplicate seats.

## API reference

| Method | Route | Description |
|---|---|---|
| `GET` | `/events` | List all events with seat counts |
| `GET` | `/events/{id}` | Get one event |
| `POST` | `/events` | Create an event, optionally generating a seat map |
| `GET` | `/events/{eventId}/seats` | List seats for an event (optional `?status=` filter) |
| `POST` | `/events/{eventId}/seats/{seatId}/reserve` | `Available → Held` |
| `POST` | `/events/{eventId}/seats/{seatId}/book` | `Held → Booked` |
| `POST` | `/events/{eventId}/seats/{seatId}/release` | `Held → Available` |

## C#/.NET concepts introduced here

- **Minimal APIs, not MVC controllers.** Routes are lambdas registered via `app.MapGet`/`MapPost`, grouped with `MapGroup("/events")` (a `RouteGroupBuilder` — prefixes every child route and lets you attach shared config like `.WithTags(...)`). Organized as extension methods on `IEndpointRouteBuilder` (`MapEventEndpoints`, `MapSeatEndpoints`) so `Program.cs` just calls `app.MapEventEndpoints()`.
- **Minimal API model binding** — a `Guid id` parameter bound from a `{id:guid}` route segment, a `TicketFlowDbContext db` parameter injected from DI, a `CreateEventRequest request` parameter deserialized from the JSON body — all inferred from the parameter's type, no `[FromRoute]`/`[FromBody]` attributes needed.
- **Primary constructors** (C# 12) — `TicketFlowDbContext(DbContextOptions<TicketFlowDbContext> options) : DbContext(options)` declares and forwards the constructor parameter in the class header.
- **Records** — every request/response DTO in `Contracts/` is a `record`: value equality and immutability for free, exactly what a wire-format type should be. Each has a static `FromX(entity)` factory method living on the record itself instead of a separate mapper class.
- **Nullable reference types** (`<Nullable>enable</Nullable>`) — `string?` vs `string` is compiler-checked.
- **EF Core migrations** via the local `dotnet-ef` tool (`.config/dotnet-tools.json` pins its version). `Program.cs` calls `db.Database.MigrateAsync()` on startup in Development, so the schema is created automatically the first time the app runs — no manual `CREATE TABLE`.
- **`.Include()` / `.AsNoTracking()`** — `db.Events.Include(e => e.Seats)` eager-loads the seat collection (without it, `Seats` is silently empty and every count comes back 0). `.AsNoTracking()` skips EF's change-tracking overhead for read-only queries.

## The concurrency-safe reservation pattern

The one piece of this phase worth remembering longest: seat status transitions don't read-then-write. They use EF Core's `ExecuteUpdateAsync` with the *old* status as part of the `WHERE` clause:

```csharp
var rowsAffected = await db.Seats
    .Where(seat => seat.Id == seatId && seat.EventId == eventId && seat.Status == SeatStatus.Available)
    .ExecuteUpdateAsync(setters => setters
        .SetProperty(seat => seat.Status, SeatStatus.Held)
        .SetProperty(seat => seat.HeldUntilUtc, heldUntil));

if (rowsAffected == 0) return Results.Conflict(...);
```

This compiles to one conditional SQL `UPDATE ... WHERE Status = 0`. Two concurrent requests for the same seat can't both get `rowsAffected == 1` — the database's own row-locking guarantees only one wins. This is deliberately the same shape gRPC + Redis will formalize when Seats becomes its own service in later phases.

**Proven, not just claimed**: `SeatEndpointsTests.Reserve_ConcurrentRequestsForSameSeat_OnlyOneSucceeds` fires 10 simultaneous reserve requests at one seat via `Task.WhenAll` and asserts exactly 1 succeeds, 9 get `409 Conflict`. Passed consistently across repeated runs.

## Testing setup and the gotcha that came out of it

`tests/TicketFlow.Api.Tests` uses xUnit + `WebApplicationFactory<Program>` — real HTTP calls into an in-process test server, hitting a real (in-memory) SQLite database, not mocks. Two things needed to make this work:

1. **`Program.cs` needs `public partial class Program;` at the end.** Top-level statements generate an `internal` `Program` class by default, invisible to another assembly; this partial declaration (no access modifier of its own) merges with the generated one as `public`, which `WebApplicationFactory<Program>` needs as its generic argument.

2. **The SQLite connection-sharing gotcha.** First attempt at `TicketFlowApiFactory` used one shared `SqliteConnection` object (`Data Source=:memory:`) reused by every `DbContext`. The concurrency test above then failed in a very informative way: **0 out of 10** requests succeeded, not "the race condition I was testing for." Root cause: `Microsoft.Data.Sqlite` connections aren't safe for concurrent command execution from multiple threads — undefined behavior, not a clean exception. Fix: SQLite's shared-cache in-memory mode —
   ```csharp
   $"Data Source={Guid.NewGuid()};Mode=Memory;Cache=Shared"
   ```
   Each `DbContext` now opens its own (thread-safe) connection, all pointed at the same named in-memory database. A separate "keeper" connection is opened once and held for the factory's lifetime purely to stop the shared in-memory DB from being destroyed the instant every other connection closes.

   **Lesson**: a single shared ADO.NET connection object is not a valid way to fake "one shared in-memory test database" under concurrent load. If a future concurrency test looks flaky or silently wrong, check the connection-sharing strategy before suspecting the application code.

## CI (`.github/workflows/ci.yaml`)

One job on `ubuntu-latest`, triggered on push/PR to `main`:

1. Checkout, setup .NET 10, cache NuGet packages (keyed on a hash of all `.csproj` files — no lock file needed).
2. `dotnet restore` + `dotnet tool restore` (pulls in `dotnet-ef`).
3. `dotnet format --verify-no-changes` — fails the build on unformatted code.
4. `dotnet ef migrations has-pending-model-changes` — fails the build if a model changed but no migration was generated for it. Verified this actually catches drift, not just that it runs.
5. `dotnet build --configuration Release`.
6. `dotnet test --configuration Release` — runs the full xUnit suite.

## Decisions and why

- **SQLite over Postgres for now.** Zero local infrastructure needed to run the API — no Docker Compose yet. Deliberately a Phase 1-only choice; Phase 2 swaps it for Postgres once there's more than one service sharing a database concern.
- **DTOs, never raw EF entities, in responses.** `Contracts/` types are the only things that leave the API. Keeps the public API shape decoupled from the database schema — a column rename shouldn't be a breaking API change.
- **Minimal APIs over MVC controllers.** Less boilerplate for an API-only service, and the modern default direction for ASP.NET Core.
- **`HeldUntilUtc` stored in the database** rather than nowhere — it's a stand-in for what becomes a Redis TTL key in Phase 5, kept here now so the reserve/hold/book flow has something real and testable before Redis exists.

## How to run

```bash
dotnet tool restore                 # once, after clone
dotnet restore && dotnet build
cd src/TicketFlow.Api && dotnet run # applies pending migrations automatically, http://localhost:5242/swagger
```

```bash
dotnet test TicketFlow.sln          # run the test suite
dotnet format --verify-no-changes   # what CI enforces for formatting
```
