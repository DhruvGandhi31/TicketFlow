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

    using var scope = app.Services.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<TicketFlowDbContext>();
    await db.Database.MigrateAsync();
}

app.UseHttpsRedirection();

app.MapEventEndpoints();
app.MapSeatEndpoints();

app.Run();
