using Microsoft.EntityFrameworkCore;
using TicketFlow.Seats.Api.Models;

namespace TicketFlow.Seats.Api.Data;

public class TicketFlowSeatsDbContext(DbContextOptions<TicketFlowSeatsDbContext> options) : DbContext(options)
{
    public DbSet<Seat> Seats => Set<Seat>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // app code already avoids dupe seats but might as well have the db back it up too.
        // EventId being the leading column also means this same index covers "all seats for
        // this event" lookups - no need for a second index just on EventId alone.
        modelBuilder.Entity<Seat>()
            .HasIndex(s => new { s.EventId, s.Section, s.Row, s.Number })
            .IsUnique();
    }
}
