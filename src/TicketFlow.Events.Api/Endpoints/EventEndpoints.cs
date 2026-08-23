using Microsoft.EntityFrameworkCore;
using TicketFlow.Events.Api.Contracts;
using TicketFlow.Events.Api.Data;
using TicketFlow.Events.Api.Models;

namespace TicketFlow.Events.Api.Endpoints;

public static class EventEndpoints
{
    public static RouteGroupBuilder MapEventEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/events").WithTags("Events");

        group.MapGet("/", async (TicketFlowEventsDbContext db) =>
        {
            var events = await db.Events.AsNoTracking().ToListAsync();
            return events.Select(EventResponse.FromEvent);
        })
        .WithName("GetEvents");

        group.MapGet("/{id:guid}", async (Guid id, TicketFlowEventsDbContext db) =>
        {
            var ev = await db.Events.AsNoTracking().FirstOrDefaultAsync(e => e.Id == id);

            return ev is null ? Results.NotFound() : Results.Ok(EventResponse.FromEvent(ev));
        })
        .WithName("GetEventById");

        group.MapPost("/", async (CreateEventRequest request, TicketFlowEventsDbContext db) =>
        {
            // quick and dirty check, swap for FluentValidation or similar if this grows any more rules
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

            db.Events.Add(ev);
            await db.SaveChangesAsync();

            // next step for the caller is a separate POST to the Seats service to actually generate the seat map
            return Results.Created($"/events/{ev.Id}", EventResponse.FromEvent(ev));
        })
        .WithName("CreateEvent");

        return group;
    }
}
