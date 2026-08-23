using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using TicketFlow.Api.Data;

namespace TicketFlow.Api.Tests;

// shared across all tests in a class via IClassFixture<TicketFlowApiFactory>, so each test class
// gets its own fresh in-memory db, built the normal way through the app's own startup migration
public class TicketFlowApiFactory : WebApplicationFactory<Program>
{
    // tried a single shared SqliteConnection object here first and it was a mess - turns out
    // Microsoft.Data.Sqlite connections aren't safe to hit from multiple threads at once, so the
    // concurrency test just got silently wrong results instead of throwing. shared-cache mode
    // fixes it: each DbContext opens its own connection but they all point at the same in-memory db.
    // guid in the name so parallel test classes don't step on each other.
    private readonly string _connectionString = $"Data Source={Guid.NewGuid()};Mode=Memory;Cache=Shared";

    // this connection never actually runs a query - it just has to stay open, otherwise the
    // in-memory db gets wiped the second every "real" connection closes
    private readonly SqliteConnection _keepAliveConnection;

    public TicketFlowApiFactory()
    {
        _keepAliveConnection = new SqliteConnection(_connectionString);
        _keepAliveConnection.Open();
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        // forces IsDevelopment() to be true so Program.cs's own migration step runs against our
        // fake db - saves duplicating schema setup logic just for tests
        builder.UseEnvironment("Development");

        builder.ConfigureServices(services =>
        {
            // swap out the real sqlite-file registration for our in-memory one
            var dbContextOptions = services.SingleOrDefault(
                d => d.ServiceType == typeof(DbContextOptions<TicketFlowDbContext>));
            if (dbContextOptions is not null)
            {
                services.Remove(dbContextOptions);
            }

            services.AddDbContext<TicketFlowDbContext>(options => options.UseSqlite(_connectionString));
        });
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
