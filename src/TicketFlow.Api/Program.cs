using Microsoft.EntityFrameworkCore;
using TicketFlow.Api.Data;
using TicketFlow.Api.Endpoints;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddDbContext<TicketFlowDbContext>(options =>
    options.UseSqlite(builder.Configuration.GetConnectionString("TicketFlow")));

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();

    // just auto-migrate on boot for now, saves running `dotnet ef database update` by hand every time
    using var scope = app.Services.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<TicketFlowDbContext>();
    await db.Database.MigrateAsync();
}

app.UseHttpsRedirection();

app.MapEventEndpoints();
app.MapSeatEndpoints();

app.Run();

// top-level statements make Program internal by default - tests need to see it for WebApplicationFactory<Program>
public partial class Program;
