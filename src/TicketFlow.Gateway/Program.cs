var builder = WebApplication.CreateBuilder(args);

// entire ReverseProxy section (routes + clusters) lives in appsettings - see there for the actual routing rules
builder.Services.AddReverseProxy()
    .LoadFromConfig(builder.Configuration.GetSection("ReverseProxy"));

var app = builder.Build();

app.MapReverseProxy();

app.Run();
