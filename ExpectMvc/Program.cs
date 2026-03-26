using ExpectMvc.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllersWithViews();

builder.Services.AddSingleton<IGitService, GitService>();
builder.Services.AddSingleton<ICookieService, CookieService>();
builder.Services.AddScoped<IAgentService, AgentService>();
builder.Services.AddScoped<IBrowserService, BrowserService>();
builder.Services.AddScoped<ITestSupervisor, TestSupervisor>();

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseRouting();
app.UseAuthorization();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

app.Run();
