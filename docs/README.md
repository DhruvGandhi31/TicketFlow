# TicketFlow docs

Personal build-log/reference for this project — one file per phase, written for future-you to come back to, not for recruiters (that's what the root `README.md` is for) and not for Claude (that's `CLAUDE.md`). Each phase doc covers what got built, why it was built that way, the .NET/C# concepts introduced along the way, and any gotchas hit while building it.

| Phase | Doc | Status |
|---|---|---|
| 0 | [Toolchain setup](phase-0-toolchain-setup.md) | Done |
| 1 | [Single API: Events + Seats](phase-1-events-seats-api.md) | Done |
| 2 | Split into services + Docker Compose + API Gateway | Not started |
| 3 | gRPC (Bookings → Seats) | Not started |
| 4 | RabbitMQ event-driven flow | Not started |
| 5 | Redis | Not started |
| 6 | Observability (Prometheus/Grafana) | Not started |
| 7 | Kubernetes | Not started |
| 8 | Full CI/CD | Not started |
| 9 | Integration tests + polish | Not started |

A new `phase-N-<slug>.md` gets added here as each phase is completed.
