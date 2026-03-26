using ClaudeAgentTeamsUI.Data;
using ClaudeAgentTeamsUI.Hubs;
using ClaudeAgentTeamsUI.Services;

var builder = WebApplication.CreateBuilder(args);

// Data store
builder.Services.AddSingleton<JsonDataStore>();

// Services
builder.Services.AddSingleton<TeamService>();
builder.Services.AddSingleton<TaskService>();
builder.Services.AddSingleton<SessionService>();
builder.Services.AddSingleton<ProjectService>();
builder.Services.AddSingleton<MessageService>();
builder.Services.AddSingleton<NotificationService>();
builder.Services.AddSingleton<ReviewService>();
builder.Services.AddSingleton<ScheduleService>();
builder.Services.AddSingleton<ConfigService>();

// MVC + SignalR
builder.Services.AddControllersWithViews();
builder.Services.AddSignalR();

var app = builder.Build();

// Wire up lazy dependencies
var teamService = app.Services.GetRequiredService<TeamService>();
var taskService = app.Services.GetRequiredService<TaskService>();
teamService.SetTaskService(taskService);

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseStaticFiles();
app.UseRouting();
app.UseAuthorization();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

app.MapHub<TeamHub>("/hubs/team");

// Map API controllers
app.MapControllers();

// Benchmark mode
if (args.Contains("--benchmark"))
{
    _ = Task.Run(async () =>
    {
        await Task.Delay(2000); // Wait for server to start
        await ClaudeAgentTeamsUI.Benchmarks.PerformanceBenchmark.RunAsync();
        Environment.Exit(0);
    });
}

app.Run();
