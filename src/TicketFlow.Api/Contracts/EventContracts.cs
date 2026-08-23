using TicketFlow.Api.Models;

namespace TicketFlow.Api.Contracts;

public record SeatMapRequest(List<string> Sections, int RowsPerSection, int SeatsPerRow);

public record CreateEventRequest(
    string Name,
    string VenueName,
    DateTime StartsAtUtc,
    string? Description,
    SeatMapRequest? SeatMap); // leave this null if you just want a bare event, no seats generated

public record EventResponse(
    Guid Id,
    string Name,
    string VenueName,
    DateTime StartsAtUtc,
    string? Description,
    int TotalSeats,
    int AvailableSeats)
{
    // heads up: this counts whatever's in e.Seats in memory, so the caller needs to have
    // .Include()'d it - otherwise both counts just silently come back as 0
    public static EventResponse FromEvent(Event e) => new(
        e.Id, e.Name, e.VenueName, e.StartsAtUtc, e.Description,
        e.Seats.Count,
        e.Seats.Count(s => s.Status == SeatStatus.Available));
}
