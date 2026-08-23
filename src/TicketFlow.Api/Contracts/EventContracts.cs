using TicketFlow.Api.Models;

namespace TicketFlow.Api.Contracts;

public record SeatMapRequest(List<string> Sections, int RowsPerSection, int SeatsPerRow);

public record CreateEventRequest(
    string Name,
    string VenueName,
    DateTime StartsAtUtc,
    string? Description,
    SeatMapRequest? SeatMap);

public record EventResponse(
    Guid Id,
    string Name,
    string VenueName,
    DateTime StartsAtUtc,
    string? Description,
    int TotalSeats,
    int AvailableSeats)
{
    public static EventResponse FromEvent(Event e) => new(
        e.Id, e.Name, e.VenueName, e.StartsAtUtc, e.Description,
        e.Seats.Count,
        e.Seats.Count(s => s.Status == SeatStatus.Available));
}
