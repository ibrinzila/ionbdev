using SpecStory.Web.Services;
using SpecStory.Web.Services.BackgroundServices;
using SpecStory.Web.Services.Providers;

var builder = WebApplication.CreateBuilder(args);

// Add MVC services
builder.Services.AddControllersWithViews()
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.PropertyNamingPolicy =
            System.Text.Json.JsonNamingPolicy.CamelCase;
        options.JsonSerializerOptions.WriteIndented = true;
    });

// Register agent providers
builder.Services.AddSingleton<IAgentProvider, ClaudeCodeProvider>();
builder.Services.AddSingleton<IAgentProvider, CursorCliProvider>();
builder.Services.AddSingleton<IAgentProvider, CodexCliProvider>();
builder.Services.AddSingleton<IAgentProvider, GeminiCliProvider>();
builder.Services.AddSingleton<IAgentProvider, DroidCliProvider>();

// Register provider factory
builder.Services.AddSingleton<ProviderFactory>(sp =>
    new ProviderFactory(sp.GetServices<IAgentProvider>()));

// Register application services
builder.Services.AddScoped<ISessionService, SessionService>();
builder.Services.AddScoped<IProviderService, ProviderService>();
builder.Services.AddSingleton<IMarkdownService, MarkdownService>();
builder.Services.AddSingleton<IConfigService, ConfigService>();

// Register cloud sync service with HttpClient
builder.Services.AddHttpClient<ICloudSyncService, CloudSyncService>(client =>
{
    client.Timeout = TimeSpan.FromSeconds(120);
    client.DefaultRequestHeaders.Add("User-Agent", "SpecStory-Web/1.0");
});

// Register background services
var config = builder.Configuration.GetSection("SpecStory");
if (config.GetValue<bool>("WatcherEnabled"))
{
    builder.Services.AddHostedService<SessionWatcherService>();
}

if (config.GetValue<bool>("CloudSyncBackgroundEnabled"))
{
    builder.Services.AddHostedService<CloudSyncBackgroundService>();
}

var app = builder.Build();

// Configure the HTTP request pipeline
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseRouting();
app.UseAuthorization();

// MVC routes
app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

// API routes are handled by attribute routing on ApiController

app.Run();
