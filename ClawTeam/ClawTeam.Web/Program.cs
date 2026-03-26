using ClawTeam.Web.Services;
using ClawTeam.Web.Spawn;

var builder = WebApplication.CreateBuilder(args);

// ── Configuration ────────────────────────────────────────────────
var dataDir = builder.Configuration["ClawTeam:DataDir"]
    ?? ConfigService.GetDataDir();
Directory.CreateDirectory(dataDir);

// ── Services (DI) ────────────────────────────────────────────────
builder.Services.AddControllersWithViews();

// Register core services as singletons (file-based state is thread-safe)
builder.Services.AddSingleton(new ConfigService());
builder.Services.AddSingleton(new TeamManager(dataDir));
builder.Services.AddSingleton<ISpawnBackend>(new SubprocessBackend());
builder.Services.AddSingleton(sp =>
    new BoardCollector(dataDir, sp.GetRequiredService<TeamManager>()));
builder.Services.AddSingleton(sp =>
    new SpawnService(dataDir, sp.GetRequiredService<TeamManager>(),
        sp.GetRequiredService<ISpawnBackend>()));

var app = builder.Build();

// ── Middleware Pipeline ──────────────────────────────────────────
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

if (!app.Environment.IsDevelopment())
{
    app.UseHttpsRedirection();
}
app.UseStaticFiles();
app.UseRouting();
app.UseAuthorization();

// ── Routes ───────────────────────────────────────────────────────
app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

app.Run();
