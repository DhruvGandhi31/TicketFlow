using TicketFlow.Seats.Api.Models;

namespace TicketFlow.Seats.Api.Contracts;

// moved here from the Events service in phase 2 - generating the seat map is Seats' job now, not Events'
public record GenerateSeatMapRequest(List<string> Sections, int RowsPerSection, int SeatsPerRow);

// deliberately not just returning the Seat entity - keeps the api shape from being glued to the db schema
public record SeatResponse(Guid Id, string Section, string Row, int Number, SeatStatus Status, DateTime? HeldUntilUtc)
{
    public static SeatResponse FromSeat(Seat s) => new(s.Id, s.Section, s.Row, s.Number, s.Status, s.HeldUntilUtc);
}
