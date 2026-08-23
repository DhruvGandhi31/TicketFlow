using Microsoft.EntityFrameworkCore;
using TicketFlow.Api.Contracts;
using TicketFlow.Api.Data;
using TicketFlow.Api.Models;

namespace TicketFlow.Api.Endpoints;

public static class EventEndpoints
{
    public static RouteGroupBuilder MapEventEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/events").WithTags("Events");

        group.MapGet("/", async (TicketFlowDbContext db) =>
        {
            var events = await db.Events.Include(e => e.Seats).AsNoTracking().ToListAsync();
            return events.Select(EventResponse.FromEvent);
        })
        .WithName("GetEvents");

        group.MapGet("/{id:guid}", async (Guid id, TicketFlowDbContext db) =>
        {
            var ev = await db.Events.Include(e => e.Seats).AsNoTracking()
                .FirstOrDefaultAsync(e => e.Id == id);

            return ev is null ? Results.NotFound() : Results.Ok(EventResponse.FromEvent(ev));
        })
        .WithName("GetEventById");

        group.MapPost("/", async (CreateEventRequest request, TicketFlowDbContext db) =>
        {
            if (string.IsNullOrWhiteSpace(request.Name) || string.IsNullOrWhiteSpace(request.VenueName))
            {
                return Results.ValidationProblem(new Dictionary<string, string[]>
                {
                    ["name/venueName"] = ["Name and venue name are required."]
                });
            }

            var ev = new Event
            {
                Name = request.Name,
                VenueName = request.VenueName,
                StartsAtUtc = request.StartsAtUtc,
                Description = request.Description
            };

            if (request.SeatMap is { } seatMap)
            {
                ev.Seats = GenerateSeatMap(ev.Id, seatMap);
            }

            db.Events.Add(ev);
            await db.SaveChangesAsync();

            return Results.Created($"/events/{ev.Id}", EventResponse.FromEvent(ev));
        })
        .WithName("CreateEvent");

        return group;
    }

    private static List<Seat> GenerateSeatMap(Guid eventId, SeatMapRequest seatMap)
    {
        var seats = new List<Seat>();

        foreach (var section in seatMap.Sections)
        {
            for (var rowIndex = 0; rowIndex < seatMap.RowsPerSection; rowIndex++)
            {
                var rowLabel = ((char)('A' + rowIndex)).ToString();

                for (var seatNumber = 1; seatNumber <= seatMap.SeatsPerRow; seatNumber++)
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

        return seats;
    }
}
