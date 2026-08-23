# TicketFlow

An event-ticketing platform built in C# / .NET, developed in phases from a single API toward a full microservices architecture (gRPC, RabbitMQ, Redis, Docker/Kubernetes, Prometheus).

[![CI](https://github.com/DhruvGandhi31/TicketFlow/actions/workflows/ci.yaml/badge.svg)](https://github.com/DhruvGandhi31/TicketFlow/actions/workflows/ci.yaml)

## What it does

TicketFlow manages events and their seat inventory: create an event with a generated seat map, browse seats, and move a seat through `Available → Held → Booked` (or release a hold). The seat-hold flow — reserve a seat, hold it for a short window, then confirm or release — is the same shape used by real ticketing systems (Ticketmaster, StubHub, etc.) to prevent double-selling a seat while a buyer is checking out.

## Current status: Phase 1 complete

This is currently a single ASP.NET Core Web API (a deliberate monolith) covering the **Events** and **Seats** domain. It is not yet split into microservices — that's the next phase. See [Roadmap](#roadmap) below for where this is headed.

## Tech stack

**In use today**
- C# 13 / .NET 10 (LTS)
- ASP.NET Core Minimal APIs
- Entity Framework Core + SQLite
- Swagger / OpenAPI (via Swashbuckle)
- xUnit + `WebApplicationFactory` integration tests (real HTTP calls against an in-memory SQLite database, including a concurrency test proving the seat-reservation race guard actually holds)
- GitHub Actions CI (build, format check, EF migration drift check, test)

**Planned (see roadmap)**
- Docker Compose → Kubernetes
- gRPC (synchronous inter-service seat reservation)
- RabbitMQ (event-driven booking/payment/notification flow)
- Redis (seat-hold TTLs, read caching)
- Prometheus + Grafana (metrics/observability)
- YARP API Gateway

## Domain model

- **Event** — `Name`, `VenueName`, `StartsAtUtc`, `Description`, and a collection of `Seat`s.
- **Seat** — belongs to an `Event`; has a `Section`/`Row`/`Number` and a `Status`:

  ```
  Available --reserve--> Held --book--> Booked
               Held --release--> Available
  ```

  A hold expires after 10 minutes (`HeldUntilUtc`); an expired hold cannot be booked.

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

Full interactive docs are available via Swagger UI when running locally (see below).

## Getting started

**Prerequisites:** [.NET 10 SDK](https://dotnet.microsoft.com/download)

```bash
# clone and enter the repo
git clone https://github.com/DhruvGandhi31/TicketFlow.git
cd TicketFlow

# restore the pinned dotnet-ef tool
dotnet tool restore

# restore & build
dotnet restore
dotnet build

# run the API (applies pending EF Core migrations automatically on startup)
cd src/TicketFlow.Api
dotnet run
```

To run the test suite instead:

```bash
dotnet test TicketFlow.sln
```

Then open `http://localhost:<port>/swagger` (the port is printed in the console output) to try the API interactively.

### Example: create an event and reserve a seat

```bash
curl -X POST http://localhost:5242/events -H "Content-Type: application/json" -d '{
  "name": "Indie Rock Night",
  "venueName": "The Grand Hall",
  "startsAtUtc": "2026-09-15T19:00:00Z",
  "seatMap": { "sections": ["GA"], "rowsPerSection": 3, "seatsPerRow": 10 }
}'

curl -X POST http://localhost:5242/events/{eventId}/seats/{seatId}/reserve
```

## Project structure

```
TicketFlow/
├── .github/workflows/ci.yaml     # build, format, migration-drift check, test
├── TicketFlow.sln
├── src/
│   └── TicketFlow.Api/
│       ├── Models/                # EF Core entities (Event, Seat)
│       ├── Data/                  # DbContext
│       ├── Contracts/             # Request/response DTOs
│       ├── Endpoints/             # Minimal API route groups
│       ├── Migrations/            # EF Core-generated schema history
│       └── Program.cs             # composition root
└── tests/
    └── TicketFlow.Api.Tests/
        ├── TicketFlowApiFactory.cs    # WebApplicationFactory: in-memory SQLite, shared-cache mode
        ├── EventEndpointsTests.cs
        └── SeatEndpointsTests.cs
```

## Roadmap

- [x] **Phase 1** — Single ASP.NET Core API (Events + Seats), EF Core, Swagger, xUnit integration tests
- [ ] **Phase 2** — Split into services (Events, Seats) + Docker Compose + API Gateway (YARP)
- [ ] **Phase 3** — gRPC for synchronous Bookings → Seats reservation
- [ ] **Phase 4** — RabbitMQ event-driven flow (Bookings, Payments, Notifications services)
- [ ] **Phase 5** — Redis (seat-hold TTLs, caching)
- [ ] **Phase 6** — Observability: Prometheus, Grafana, structured logging, health checks
- [ ] **Phase 7** — Kubernetes manifests/Helm
- [ ] **Phase 8** — Full GitHub Actions CI/CD (image build/push, deploy)
- [ ] **Phase 9** — Integration tests (Testcontainers), polish
