using Codapter.BackendPi;
using Codapter.Core.Models;
using Codapter.Core.Services;
using Codapter.Web.Hubs;
using Codapter.Web.Middleware;
using Codapter.Web.Services;

var builder = WebApplication.CreateBuilder(args);

// Configuration
var collabEnabled = !string.IsNullOrEmpty(
    Environment.GetEnvironmentVariable("CODAPTER_COLLAB"));

var piCommand = Environment.GetEnvironmentVariable("CODAPTER_PI_COMMAND") ?? "npx";
var piIdleTimeout = int.TryParse(
    Environment.GetEnvironmentVariable("CODAPTER_PI_IDLE_TIMEOUT_MS"), out var timeout)
    ? timeout
    : 300_000;

// Services
builder.Services.AddControllersWithViews();
builder.Services.AddSignalR()
    .AddJsonProtocol(options =>
    {
        options.PayloadSerializerOptions.PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase;
        options.PayloadSerializerOptions.DefaultIgnoreCondition =
            System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull;
    });

// Core services
builder.Services.AddSingleton<ThreadRegistry>(sp =>
{
    var logger = sp.GetService<ILogger<ThreadRegistry>>();
    return new ThreadRegistry(logger: logger);
});

builder.Services.AddSingleton<InMemoryConfigStore>();

builder.Services.AddSingleton<CommandExecManager>(sp =>
{
    var logger = sp.GetService<ILogger<CommandExecManager>>();
    return new CommandExecManager(logger);
});

// Pi Backend
builder.Services.AddSingleton<IBackend>(sp =>
{
    var logger = sp.GetService<ILogger<PiBackend>>();
    return new PiBackend(new PiBackendOptions
    {
        Command = piCommand,
        IdleTimeoutMs = piIdleTimeout,
        DebugLogFile = Environment.GetEnvironmentVariable("CODAPTER_DEBUG_LOG_FILE")
    }, logger);
});

// App Server Connection (scoped per SignalR connection or request)
builder.Services.AddSingleton<AppServerConnection>(sp =>
{
    var backend = sp.GetRequiredService<IBackend>();
    var threadRegistry = sp.GetRequiredService<ThreadRegistry>();
    var configStore = sp.GetRequiredService<InMemoryConfigStore>();
    var commandExec = sp.GetRequiredService<CommandExecManager>();
    var logger = sp.GetRequiredService<ILogger<AppServerConnection>>();
    return new AppServerConnection(
        backend, threadRegistry, configStore, commandExec, logger, collabEnabled);
});

// CORS for development
builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        policy.AllowAnyOrigin().AllowAnyMethod().AllowAnyHeader();
    });
});

var app = builder.Build();

// Initialize services
var threadRegistry = app.Services.GetRequiredService<ThreadRegistry>();
await threadRegistry.LoadAsync();

var configStore = app.Services.GetRequiredService<InMemoryConfigStore>();
await configStore.LoadAsync();

if (app.Services.GetRequiredService<IBackend>() is PiBackend piBackend)
{
    await piBackend.InitializeAsync();
}

// Middleware pipeline
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseRouting();
app.UseCors();

app.UseWebSockets();
app.UseWebSocketRpc();

// Endpoints
app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

app.MapHub<RpcHub>("/hub/rpc");

// Log startup
var logger = app.Services.GetRequiredService<ILogger<Program>>();
var urls = string.Join(", ", app.Urls);
logger.LogInformation("Codapter C# MVC started. Endpoints: {Urls}", urls);
logger.LogInformation("Collaboration: {Collab}", collabEnabled ? "enabled" : "disabled");

app.Run();
