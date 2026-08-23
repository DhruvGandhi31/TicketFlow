using TicketFlow.Events.Api.Models;

namespace TicketFlow.Events.Api.Contracts;

// no SeatMap field anymore - creating an event and populating its seat map are two separate calls
// now (Events service vs Seats service). the client hits both.
public record CreateEventRequest(string Name, string VenueName, DateTime StartsAtUtc, string? Description);

// dropped TotalSeats/AvailableSeats from phase 1 - can't compute them here anymore without a cross-service
// call, and faking it felt worse than just not having it yet. this is exactly the itch phase 3's gRPC scratches.
public record EventResponse(Guid Id, string Name, string VenueName, DateTime StartsAtUtc, string? Description)
{
    public static EventResponse FromEvent(Event e) => new(e.Id, e.Name, e.VenueName, e.StartsAtUtc, e.Description);
}
