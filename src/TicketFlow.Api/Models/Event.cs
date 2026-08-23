namespace TicketFlow.Api.Models;

public class Event
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public required string Name { get; set; }
    public required string VenueName { get; set; }
    public DateTime StartsAtUtc { get; set; }
    public string? Description { get; set; }

    public List<Seat> Seats { get; set; } = [];
}
