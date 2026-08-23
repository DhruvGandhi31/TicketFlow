using Microsoft.EntityFrameworkCore;
using TicketFlow.Seats.Api.Contracts;
using TicketFlow.Seats.Api.Data;
using TicketFlow.Seats.Api.Models;

namespace TicketFlow.Seats.Api.Endpoints;

public static class SeatEndpoints
{
    private static readonly TimeSpan HoldDuration = TimeSpan.FromMinutes(10);

    public static RouteGroupBuilder MapSeatEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/events/{eventId:guid}/seats").WithTags("Seats");

        // moved over from the Events service - Seats owns the seat map now, Events just owns the event itself.
        // doesn't check the event actually exists (no way to, without calling the Events service) - that
        // cross-service check is exactly what phase 3's gRPC call is for
        group.MapPost("/", async (Guid eventId, GenerateSeatMapRequest request, TicketFlowSeatsDbContext db) =>
        {
            var seats = new List<Seat>();

            foreach (var section in request.Sections)
            {
                for (var rowIndex = 0; rowIndex < request.RowsPerSection; rowIndex++)
                {
                    var rowLabel = ((char)('A' + rowIndex)).ToString(); // breaks past row 26 (Z), not worrying about it yet

                    for (var seatNumber = 1; seatNumber <= request.SeatsPerRow; seatNumber++)
                    {
                        seats.Add(new Seat
                        {
                            EventId = eventId,
                            Section = section,
                            Row = rowLabel,
                            Number = seatNumber
                        });
                    }
                }
            }

            db.Seats.AddRange(seats);
            await db.SaveChangesAsync();

            return Results.Created($"/events/{eventId}/seats", seats.Select(SeatResponse.FromSeat));
        })
        .WithName("GenerateSeatMap");

        group.MapGet("/", async (Guid eventId, SeatStatus? status, TicketFlowSeatsDbContext db) =>
        {
            var query = db.Seats.Where(s => s.EventId == eventId).AsNoTracking();
            if (status is { } s)
            {
                query = query.Where(seat => seat.Status == s);
            }

            var seats = await query.OrderBy(seat => seat.Section).ThenBy(seat => seat.Row).ThenBy(seat => seat.Number)
                .ToListAsync();

            return seats.Select(SeatResponse.FromSeat);
        })
        .WithName("GetSeats");

        // available -> held. the WHERE Status = Available is the whole trick here - if two requests
        // hit this at the same time only one of them actually matches the row, the other gets 0 back
        group.MapPost("/{seatId:guid}/reserve", async (Guid eventId, Guid seatId, TicketFlowSeatsDbContext db) =>
        {
            var heldUntil = DateTime.UtcNow.Add(HoldDuration);

            var rowsAffected = await db.Seats
                .Where(seat => seat.Id == seatId && seat.EventId == eventId && seat.Status == SeatStatus.Available)
                .ExecuteUpdateAsync(setters => setters
                    .SetProperty(seat => seat.Status, SeatStatus.Held)
                    .SetProperty(seat => seat.HeldUntilUtc, heldUntil));

            if (rowsAffected == 0)
            {
                return Results.Conflict(new { message = "Seat is not available to reserve." });
            }

            var seat = await db.Seats.AsNoTracking().FirstAsync(x => x.Id == seatId);
            return Results.Ok(SeatResponse.FromSeat(seat));
        })
        .WithName("ReserveSeat");

        // held (and hold hasn't expired) -> booked
        group.MapPost("/{seatId:guid}/book", async (Guid eventId, Guid seatId, TicketFlowSeatsDbContext db) =>
        {
            var now = DateTime.UtcNow;

            var rowsAffected = await db.Seats
                .Where(seat => seat.Id == seatId && seat.EventId == eventId
                    && seat.Status == SeatStatus.Held && seat.HeldUntilUtc > now)
                .ExecuteUpdateAsync(setters => setters
                    .SetProperty(seat => seat.Status, SeatStatus.Booked)
                    .SetProperty(seat => seat.HeldUntilUtc, (DateTime?)null));

            if (rowsAffected == 0)
            {
                // don't really care here whether it was never held or the hold just timed out, same response either way
                return Results.Conflict(new { message = "Seat must have an active hold before it can be booked." });
            }

            var seat = await db.Seats.AsNoTracking().FirstAsync(x => x.Id == seatId);
            return Results.Ok(SeatResponse.FromSeat(seat));
        })
        .WithName("BookSeat");

        // held -> available, for when someone bails on checkout instead of confirming
        group.MapPost("/{seatId:guid}/release", async (Guid eventId, Guid seatId, TicketFlowSeatsDbContext db) =>
        {
            var rowsAffected = await db.Seats
                .Where(seat => seat.Id == seatId && seat.EventId == eventId && seat.Status == SeatStatus.Held)
                .ExecuteUpdateAsync(setters => setters
                    .SetProperty(seat => seat.Status, SeatStatus.Available)
                    .SetProperty(seat => seat.HeldUntilUtc, (DateTime?)null));

            if (rowsAffected == 0)
            {
                return Results.Conflict(new { message = "Seat is not currently held." });
            }

            var seat = await db.Seats.AsNoTracking().FirstAsync(x => x.Id == seatId);
            return Results.Ok(SeatResponse.FromSeat(seat));
        })
        .WithName("ReleaseSeat");

        return group;
    }
}
