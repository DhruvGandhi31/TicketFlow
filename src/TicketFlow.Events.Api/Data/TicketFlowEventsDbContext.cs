using Microsoft.EntityFrameworkCore;
using TicketFlow.Events.Api.Models;

namespace TicketFlow.Events.Api.Data;

public class TicketFlowEventsDbContext(DbContextOptions<TicketFlowEventsDbContext> options) : DbContext(options)
{
    public DbSet<Event> Events => Set<Event>();
}
