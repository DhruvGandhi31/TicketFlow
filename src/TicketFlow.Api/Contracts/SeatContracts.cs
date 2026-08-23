using TicketFlow.Api.Models;

namespace TicketFlow.Api.Contracts;

// deliberately not just returning the Seat entity - keeps the api shape from being glued to the db schema
public record SeatResponse(Guid Id, string Section, string Row, int Number, SeatStatus Status, DateTime? HeldUntilUtc)
{
    public static SeatResponse FromSeat(Seat s) => new(s.Id, s.Section, s.Row, s.Number, s.Status, s.HeldUntilUtc);
}
