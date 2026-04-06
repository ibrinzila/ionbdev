using EdgeParse.Core.Api;
using EdgeParse.Core.Models;

var builder = WebApplication.CreateBuilder(args);

// Add services
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new Microsoft.OpenApi.Models.OpenApiInfo
    {
        Title = "EdgeParse API",
        Version = "v1",
        Description = "High-performance PDF-to-structured-data extraction engine. " +
                      "Reads the geometry baked into every PDF — coordinates, font metrics, " +
                      "spatial relationships — and reconstructs the document as it was meant to be read."
    });
});

// Configure default processing config from appsettings
builder.Services.Configure<ProcessingConfig>(
    builder.Configuration.GetSection("EdgeParse"));

// CORS for browser-based clients
builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        policy.AllowAnyOrigin()
              .AllowAnyMethod()
              .AllowAnyHeader();
    });
});

var app = builder.Build();

// Configure pipeline
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseCors();
app.MapControllers();

// Health check
app.MapGet("/health", () => Results.Ok(new { status = "healthy", service = "edgeparse" }));

app.Run();
