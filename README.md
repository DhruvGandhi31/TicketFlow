# TicketFlow

An event-ticketing platform built in C# / .NET, developed in phases from a single API toward a full microservices architecture (gRPC, RabbitMQ, Redis, Docker/Kubernetes, Prometheus).

[![CI](https://github.com/DhruvGandhi31/TicketFlow/actions/workflows/ci.yaml/badge.svg)](https://github.com/DhruvGandhi31/TicketFlow/actions/workflows/ci.yaml)

## What it does

TicketFlow manages events and their seat inventory: create an event, generate a seat map for it, browse seats, and move a seat through `Available → Held → Booked` (or release a hold). The seat-hold flow — reserve a seat, hold it for a short window, then confirm or release — is the same shape used by real ticketing systems (Ticketmaster, StubHub, etc.) to prevent double-selling a seat while a buyer is checking out.

## Current status: Phase 2 complete

Three independently deployable services behind a gateway, each with its own database — verified end-to-end with `docker compose up` (create event → generate seat map → reserve → book, all routed correctly through the gateway to the right service).

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

Events and Seats can no longer see each other's data directly — no shared database, no cross-service foreign keys. `EventResponse` lost the seat-count fields it had in Phase 1 as a result; composing that back together across two services is exactly what Phase 3's gRPC call is for. See [`docs/phase-2-microservices-split.md`](docs/phase-2-microservices-split.md) for the full story, including a couple of EF Core gotchas hit while splitting the test suite.

## Tech stack

**In use today**
- C# 13 / .NET 10 (LTS)
- ASP.NET Core Minimal APIs
- Entity Framework Core + PostgreSQL (one Postgres instance, one database per service)
- YARP reverse proxy as an API Gateway
- Docker + Docker Compose
- Swagger / OpenAPI (via Swashbuckle) per service
- xUnit + `WebApplicationFactory` integration tests (SQLite in-memory, not Postgres — see the docs for why), including a concurrency test proving the seat-reservation race guard actually holds
- GitHub Actions CI (build, format check, EF migration drift check per service, test, Docker image build validation)

**Planned (see roadmap)**
- Kubernetes
- gRPC (synchronous inter-service seat reservation)
- RabbitMQ (event-driven booking/payment/notification flow)
- Redis (seat-hold TTLs, read caching)
- Prometheus + Grafana (metrics/observability)

## Domain model

- **Event** (Events service) — `Name`, `VenueName`, `StartsAtUtc`, `Description`.
- **Seat** (Seats service) — `EventId` (a plain value, not a foreign key — it points at a row in a different service's database), `Section`/`Row`/`Number`, and a `Status`:

  ```
  Available --reserve--> Held --book--> Booked
               Held --release--> Available
  ```

  A hold expires after 10 minutes (`HeldUntilUtc`); an expired hold cannot be booked.

## API reference

All routes below are hit through the **gateway** (port 5100 locally, or 8080 inside Docker) — clients only ever talk to one address; the gateway routes to whichever service owns that data.

| Method | Route | Owning service |
|---|---|---|
| `GET` | `/events` | Events |
| `GET` | `/events/{id}` | Events |
| `POST` | `/events` | Events |
| `POST` | `/events/{eventId}/seats` | Seats — generates the seat map |
| `GET` | `/events/{eventId}/seats` | Seats — optional `?status=` filter |
| `POST` | `/events/{eventId}/seats/{seatId}/reserve` | Seats — `Available → Held` |
| `POST` | `/events/{eventId}/seats/{seatId}/book` | Seats — `Held → Booked` |
| `POST` | `/events/{eventId}/seats/{seatId}/release` | Seats — `Held → Available` |

Each service also has its own Swagger UI when run in Development mode (see below).

## Getting started

**Prerequisites:** [.NET 10 SDK](https://dotnet.microsoft.com/download), [Docker](https://www.docker.com/) (with Docker Compose)

### Run everything with Docker Compose (recommended)

```bash
git clone https://github.com/DhruvGandhi31/TicketFlow.git
cd TicketFlow
docker compose up --build
```

This starts Postgres (with `events` and `seats` databases created automatically), both APIs, and the gateway. Once it's up:

- Gateway (the one address a client needs): `http://localhost:5100`
- Events service directly + Swagger: `http://localhost:5101/swagger`
- Seats service directly + Swagger: `http://localhost:5102/swagger`

### Run a service locally without Docker

```bash
docker compose up postgres   # just the database
dotnet tool restore          # once, after clone
dotnet restore && dotnet build
cd src/TicketFlow.Events.Api && dotnet run   # or TicketFlow.Seats.Api / TicketFlow.Gateway
```

### Run the test suite

```bash
dotnet test TicketFlow.sln
```

### Example: create an event, generate seats, reserve one — through the gateway

```bash
EVENT=$(curl -s -X POST http://localhost:5100/events -H "Content-Type: application/json" -d '{
  "name": "Indie Rock Night",
  "venueName": "The Grand Hall",
  "startsAtUtc": "2026-09-15T19:00:00Z"
}')
EVENT_ID=$(echo "$EVENT" | grep -o '"id":"[^"]*"' | cut -d'"' -f4)

curl -X POST http://localhost:5100/events/$EVENT_ID/seats -H "Content-Type: application/json" -d '{
  "sections": ["GA"], "rowsPerSection": 3, "seatsPerRow": 10
}'

curl -X POST http://localhost:5100/events/$EVENT_ID/seats/{seatId}/reserve
```

## Project structure

```
TicketFlow/
├── .github/workflows/ci.yaml     # build, format, per-service migration-drift check, test, docker build
├── docker-compose.yml
├── db/init/                      # postgres init script: creates the events + seats databases
├── TicketFlow.sln
├── src/
│   ├── TicketFlow.Events.Api/    # owns Event data + its own postgres db
│   ├── TicketFlow.Seats.Api/     # owns Seat data + its own postgres db
│   └── TicketFlow.Gateway/       # YARP reverse proxy, routes /events/**/seats/** vs /events/**
└── tests/
    ├── TicketFlow.Events.Api.Tests/
    └── TicketFlow.Seats.Api.Tests/
```

Each service project follows the same internal shape: `Models/`, `Data/` (DbContext), `Contracts/` (DTOs), `Endpoints/` (minimal API route groups), `Migrations/`, `Program.cs`, `Dockerfile`.

## Roadmap

- [x] **Phase 1** — Single ASP.NET Core API (Events + Seats), EF Core, Swagger, xUnit integration tests
- [x] **Phase 2** — Split into services (Events, Seats) + Docker Compose + API Gateway (YARP)
- [ ] **Phase 3** — gRPC for synchronous Bookings → Seats reservation
- [ ] **Phase 4** — RabbitMQ event-driven flow (Bookings, Payments, Notifications services)
- [ ] **Phase 5** — Redis (seat-hold TTLs, caching)
- [ ] **Phase 6** — Observability: Prometheus, Grafana, structured logging, health checks
- [ ] **Phase 7** — Kubernetes manifests/Helm
- [ ] **Phase 8** — Full GitHub Actions CI/CD (image build/push, deploy)
- [ ] **Phase 9** — Integration tests (Testcontainers), polish
