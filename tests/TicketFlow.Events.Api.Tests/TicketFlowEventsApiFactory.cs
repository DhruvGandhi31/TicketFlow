using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using TicketFlow.Events.Api.Data;

namespace TicketFlow.Events.Api.Tests;

// shared across all tests in a class via IClassFixture<TicketFlowEventsApiFactory>, so each test
// class gets its own fresh in-memory db
public class TicketFlowEventsApiFactory : WebApplicationFactory<Program>
{
    // shared-cache mode so every DbContext gets its own (thread-safe) connection while all of them
    // still see the same in-memory database - see the seats test factory for the full story on why
    private readonly string _connectionString = $"Data Source={Guid.NewGuid()};Mode=Memory;Cache=Shared";
    private readonly SqliteConnection _keepAliveConnection;

    public TicketFlowEventsApiFactory()
    {
        _keepAliveConnection = new SqliteConnection(_connectionString);
        _keepAliveConnection.Open();
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        // matches the "Testing" check in Program.cs that skips the UseNpgsql call entirely
        builder.UseEnvironment("Testing");

        builder.ConfigureServices(services =>
        {
            var dbContextOptions = services.SingleOrDefault(
                d => d.ServiceType == typeof(DbContextOptions<TicketFlowEventsDbContext>));
            if (dbContextOptions is not null)
            {
                services.Remove(dbContextOptions);
            }

            services.AddDbContext<TicketFlowEventsDbContext>(options => options.UseSqlite(_connectionString));
        });
    }

    // the real migrations have postgres-only column types baked in (uuid, timestamp with time
    // zone), so running them against sqlite isn't safe to assume works. EnsureCreated just builds
    // the schema straight off the current model instead - tests don't care about migration
    // history, only that the schema matches what the code expects right now.
    protected override IHost CreateHost(IHostBuilder builder)
    {
        var host = base.CreateHost(builder);

        using var scope = host.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<TicketFlowEventsDbContext>();
        db.Database.EnsureCreated();

        return host;
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        if (disposing)
        {
            _keepAliveConnection.Dispose();
        }
    }
}
