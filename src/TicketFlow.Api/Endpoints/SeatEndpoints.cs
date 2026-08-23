using Microsoft.EntityFrameworkCore;
using TicketFlow.Api.Contracts;
using TicketFlow.Api.Data;
using TicketFlow.Api.Models;

namespace TicketFlow.Api.Endpoints;

public static class SeatEndpoints
{
    private static readonly TimeSpan HoldDuration = TimeSpan.FromMinutes(10);

    public static RouteGroupBuilder MapSeatEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/events/{eventId:guid}/seats").WithTags("Seats");

        group.MapGet("/", async (Guid eventId, SeatStatus? status, TicketFlowDbContext db) =>
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

        // Available -> Held. Uses a conditional UPDATE (WHERE Status = Available) so two concurrent
        // requests for the same seat can't both succeed - only one row-affected count comes back as 1.
        group.MapPost("/{seatId:guid}/reserve", async (Guid eventId, Guid seatId, TicketFlowDbContext db) =>
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

        // Held (and not expired) -> Booked.
        group.MapPost("/{seatId:guid}/book", async (Guid eventId, Guid seatId, TicketFlowDbContext db) =>
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
                return Results.Conflict(new { message = "Seat must have an active hold before it can be booked." });
            }

            var seat = await db.Seats.AsNoTracking().FirstAsync(x => x.Id == seatId);
            return Results.Ok(SeatResponse.FromSeat(seat));
        })
        .WithName("BookSeat");

        // Held -> Available (cancel a hold, e.g. the user abandoned checkout).
        group.MapPost("/{seatId:guid}/release", async (Guid eventId, Guid seatId, TicketFlowDbContext db) =>
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
