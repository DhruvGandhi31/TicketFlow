namespace TicketFlow.Seats.Api.Models;

// heads up: this gets stored as a plain int (0/1/2) in postgres, so raw db/json dumps show numbers not names
public enum SeatStatus
{
    Available,
    Held,
    Booked
}

public class Seat
{
    public Guid Id { get; set; } = Guid.NewGuid();

    // just a plain guid, not a foreign key - Event lives in a different service's database now,
    // can't have a real FK across service boundaries. this service trusts the caller to pass a real event id.
    public Guid EventId { get; set; }

    public required string Section { get; set; }
    public required string Row { get; set; }
    public int Number { get; set; }

    public SeatStatus Status { get; set; } = SeatStatus.Available;

    // only set while Held. nothing actively expires this yet, book/reserve just check it manually.
    // redis ttl will probably replace this whole column down the line
    public DateTime? HeldUntilUtc { get; set; }
}
