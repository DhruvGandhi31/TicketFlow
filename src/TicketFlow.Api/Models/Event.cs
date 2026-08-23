namespace TicketFlow.Api.Models;

public class Event
{
    // gen the id here instead of letting the db do it, so we already have it before SaveChanges runs
    public Guid Id { get; set; } = Guid.NewGuid();
    public required string Name { get; set; }
    public required string VenueName { get; set; }
    public DateTime StartsAtUtc { get; set; }
    public string? Description { get; set; } // only optional field on this one

    public List<Seat> Seats { get; set; } = [];
}
