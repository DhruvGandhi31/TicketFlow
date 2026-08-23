using Microsoft.EntityFrameworkCore;
using TicketFlow.Api.Models;

namespace TicketFlow.Api.Data;

public class TicketFlowDbContext(DbContextOptions<TicketFlowDbContext> options) : DbContext(options)
{
    public DbSet<Event> Events => Set<Event>();
    public DbSet<Seat> Seats => Set<Seat>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // app code already avoids dupe seats but might as well have the db back it up too
        modelBuilder.Entity<Seat>()
            .HasIndex(s => new { s.EventId, s.Section, s.Row, s.Number })
            .IsUnique();

        modelBuilder.Entity<Seat>()
            .HasOne(s => s.Event)
            .WithMany(e => e.Seats)
            .HasForeignKey(s => s.EventId)
            .OnDelete(DeleteBehavior.Cascade); // kill the event, kill its seats, don't leave orphans around
    }
}
