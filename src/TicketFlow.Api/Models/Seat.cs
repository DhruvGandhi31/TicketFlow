namespace TicketFlow.Api.Models;

public enum SeatStatus
{
    Available,
    Held,
    Booked
}

public class Seat
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid EventId { get; set; }
    public Event? Event { get; set; }

    public required string Section { get; set; }
    public required string Row { get; set; }
    public int Number { get; set; }

    public SeatStatus Status { get; set; } = SeatStatus.Available;

    // Null unless Status == Held; a background sweep (or a future Redis TTL) releases the seat once this passes.
    public DateTime? HeldUntilUtc { get; set; }
}
