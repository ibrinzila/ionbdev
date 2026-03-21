using Water.Core;
using Water.Observability;
using Water.Server;
using Water.Storage;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();

// Register Water services
builder.Services.AddSingleton<FlowRegistry>();
builder.Services.AddSingleton<IStorageBackend, InMemoryStorage>();
builder.Services.AddSingleton(sp => new FlowDashboard(sp.GetService<IStorageBackend>()));

// Add CORS for development
builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        policy.AllowAnyOrigin().AllowAnyMethod().AllowAnyHeader();
    });
});

var app = builder.Build();

app.UseCors();
app.MapControllers();

// Map health check at root
app.MapGet("/", () => Results.Ok(new
{
    name = "Water Flows API",
    version = "1.0.0",
    status = "running"
}));

app.Run();
