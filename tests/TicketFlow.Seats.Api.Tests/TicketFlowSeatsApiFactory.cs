using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using TicketFlow.Seats.Api.Data;

namespace TicketFlow.Seats.Api.Tests;

// shared across all tests in a class via IClassFixture<TicketFlowSeatsApiFactory>, so each test
// class gets its own fresh in-memory db
public class TicketFlowSeatsApiFactory : WebApplicationFactory<Program>
{
    // a plain "Data Source=:memory:" connection is ONE connection object, and Microsoft.Data.Sqlite
    // connections aren't safe for concurrent command execution from multiple threads - learned this
    // the hard way in phase 1's concurrency test (got 0/10 successes instead of 1/10, silently
    // wrong, no exception). shared-cache mode fixes it: every DbContext opens its own connection,
    // all pointed at the same named in-memory db. guid in the name keeps parallel test classes apart.
    private readonly string _connectionString = $"Data Source={Guid.NewGuid()};Mode=Memory;Cache=Shared";

    // never queried directly - just has to stay open, or the in-memory db gets wiped the moment
    // every "real" connection closes
    private readonly SqliteConnection _keepAliveConnection;

    public TicketFlowSeatsApiFactory()
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
                d => d.ServiceType == typeof(DbContextOptions<TicketFlowSeatsDbContext>));
            if (dbContextOptions is not null)
            {
                services.Remove(dbContextOptions);
            }

            services.AddDbContext<TicketFlowSeatsDbContext>(options => options.UseSqlite(_connectionString));
        });
    }

    // real migrations have postgres-only column types baked in (uuid, timestamp with time zone) -
    // not safe to assume those apply cleanly to sqlite. EnsureCreated builds the schema straight
    // off the current model instead, which is all a test actually needs.
    protected override IHost CreateHost(IHostBuilder builder)
    {
        var host = base.CreateHost(builder);

        using var scope = host.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<TicketFlowSeatsDbContext>();
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
