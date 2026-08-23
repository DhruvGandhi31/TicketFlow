using Microsoft.EntityFrameworkCore;
using TicketFlow.Seats.Api.Data;
using TicketFlow.Seats.Api.Endpoints;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddDbContext<TicketFlowSeatsDbContext>(options =>
{
    // tests swap this for sqlite (see TicketFlowSeatsApiFactory). doing the swap with an "if" here
    // instead of just overriding the registration in the test host, because UseNpgsql/UseSqlite
    // both register their own provider services as a side effect - if both end up registered at
    // once ef throws "only a single database provider can be registered". easiest fix: never call
    // UseNpgsql in the first place when running under the test host's "Testing" environment.
    if (!builder.Environment.IsEnvironment("Testing"))
    {
        options.UseNpgsql(builder.Configuration.GetConnectionString("TicketFlowSeats"));
    }
});

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();

    // just auto-migrate on boot for now, saves running `dotnet ef database update` by hand every time
    using var scope = app.Services.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<TicketFlowSeatsDbContext>();
    await db.Database.MigrateAsync();
}

// no https redirect - this runs behind the gateway on a private docker network with no cert configured,
// tls termination (if any) would happen at the edge, not per-service
app.MapSeatEndpoints();

app.Run();

// top-level statements make Program internal by default - tests need to see it for WebApplicationFactory<Program>
public partial class Program;
