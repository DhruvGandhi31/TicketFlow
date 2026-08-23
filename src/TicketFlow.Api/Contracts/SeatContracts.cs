using TicketFlow.Api.Models;

namespace TicketFlow.Api.Contracts;

public record SeatResponse(Guid Id, string Section, string Row, int Number, SeatStatus Status, DateTime? HeldUntilUtc)
{
    public static SeatResponse FromSeat(Seat s) => new(s.Id, s.Section, s.Row, s.Number, s.Status, s.HeldUntilUtc);
}
