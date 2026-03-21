using Water.Core;
using Water.Observability;
using Water.Server;
using Water.Storage;
using TaskFactory = Water.Core.TaskFactory;

var builder = WebApplication.CreateBuilder(args);

// Add MVC with Razor Views + API controllers
builder.Services.AddControllersWithViews();
builder.Services.AddEndpointsApiExplorer();

// Register Water services
builder.Services.AddSingleton<IStorageBackend, InMemoryStorage>();
builder.Services.AddSingleton(sp => new FlowDashboard(sp.GetRequiredService<IStorageBackend>()));
builder.Services.AddSingleton<FlowRegistry>(sp =>
{
    var registry = new FlowRegistry();
    var storage = sp.GetRequiredService<IStorageBackend>();

    // --- Demo Flows ---

    // 1. Data Processing Pipeline
    var normalize = TaskFactory.CreateSync(
        (input, ctx) =>
        {
            var data = input.TryGetValue("input_data", out var d) && d is Dictionary<string, object?> dict ? dict : input;
            var text = data.TryGetValue("text", out var t) ? t?.ToString()?.Trim().ToLowerInvariant() ?? "" : "";
            return new Dictionary<string, object?> { ["text"] = text, ["char_count"] = text.Length };
        },
        id: "normalize",
        description: "Normalize and clean input text");

    var wordCount = TaskFactory.CreateSync(
        (input, ctx) =>
        {
            var data = input.TryGetValue("input_data", out var d) && d is Dictionary<string, object?> dict ? dict : input;
            var text = data.TryGetValue("text", out var t) ? t?.ToString() ?? "" : "";
            var words = text.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            return new Dictionary<string, object?>(data) { ["word_count"] = words.Length, ["words"] = words.ToList() as object };
        },
        id: "word_count",
        description: "Count words in text");

    var summarize = TaskFactory.CreateSync(
        (input, ctx) =>
        {
            var data = input.TryGetValue("input_data", out var d) && d is Dictionary<string, object?> dict ? dict : input;
            var charCount = data.TryGetValue("char_count", out var cc) && cc is int c ? c : 0;
            var wordCountVal = data.TryGetValue("word_count", out var wc) && wc is int w ? w : 0;
            return new Dictionary<string, object?>(data)
            {
                ["summary"] = $"Processed {wordCountVal} words ({charCount} chars)",
                ["processed_at"] = DateTime.UtcNow.ToString("O")
            };
        },
        id: "summarize",
        description: "Generate processing summary");

    var dataFlow = new Flow(id: "data_pipeline", description: "Text data processing pipeline", storage: storage)
        .SetMetadata("category", "data")
        .SetMetadata("version", "1.0")
        .Then(normalize)
        .Then(wordCount)
        .Then(summarize);

    registry.Register(dataFlow);

    // 2. Parallel Analysis Flow
    var sentimentTask = TaskFactory.CreateSync(
        (input, ctx) =>
        {
            var data = input.TryGetValue("input_data", out var d) && d is Dictionary<string, object?> dict ? dict : input;
            var text = data.TryGetValue("text", out var t) ? t?.ToString() ?? "" : "";
            var score = text.Contains("good") || text.Contains("great") || text.Contains("happy") ? 0.8 : text.Contains("bad") || text.Contains("terrible") ? -0.7 : 0.1;
            return new Dictionary<string, object?> { ["sentiment_score"] = score, ["sentiment"] = score > 0.3 ? "positive" : score < -0.3 ? "negative" : "neutral" };
        },
        id: "sentiment",
        description: "Analyze text sentiment");

    var keywordsTask = TaskFactory.CreateSync(
        (input, ctx) =>
        {
            var data = input.TryGetValue("input_data", out var d) && d is Dictionary<string, object?> dict ? dict : input;
            var text = data.TryGetValue("text", out var t) ? t?.ToString() ?? "" : "";
            var keywords = text.Split(' ', StringSplitOptions.RemoveEmptyEntries)
                .Where(w => w.Length > 4)
                .Distinct()
                .Take(5)
                .ToList();
            return new Dictionary<string, object?> { ["keywords"] = keywords as object };
        },
        id: "keywords",
        description: "Extract keywords from text");

    var languageTask = TaskFactory.CreateSync(
        (input, ctx) => new Dictionary<string, object?> { ["language"] = "en", ["confidence"] = 0.95 },
        id: "language",
        description: "Detect language");

    var analysisFlow = new Flow(id: "parallel_analysis", description: "Run sentiment, keyword, and language analysis in parallel", storage: storage)
        .SetMetadata("category", "analysis")
        .SetMetadata("version", "1.0")
        .Parallel(new List<WaterTask> { sentimentTask, keywordsTask, languageTask });

    registry.Register(analysisFlow);

    // 3. Branching Router Flow
    var highPriority = TaskFactory.CreateSync(
        (input, ctx) => new Dictionary<string, object?> { ["queue"] = "urgent", ["handler"] = "senior_agent", ["priority"] = "high" },
        id: "high_priority",
        description: "Route to high-priority queue");

    var normalPriority = TaskFactory.CreateSync(
        (input, ctx) => new Dictionary<string, object?> { ["queue"] = "standard", ["handler"] = "auto_agent", ["priority"] = "normal" },
        id: "normal_priority",
        description: "Route to standard queue");

    var lowPriority = TaskFactory.CreateSync(
        (input, ctx) => new Dictionary<string, object?> { ["queue"] = "batch", ["handler"] = "batch_processor", ["priority"] = "low" },
        id: "low_priority",
        description: "Route to batch queue");

    var routerFlow = new Flow(id: "priority_router", description: "Route requests based on priority score", storage: storage)
        .SetMetadata("category", "routing")
        .Branch(new List<(Func<Dictionary<string, object?>, bool>, WaterTask)>
        {
            (data => data.TryGetValue("score", out var s) && s is int score && score >= 80, highPriority),
            (data => data.TryGetValue("score", out var s) && s is int score && score >= 40, normalPriority),
            (data => true, lowPriority)
        });

    registry.Register(routerFlow);

    // 4. Retry Loop Flow
    var incrementTask = TaskFactory.CreateSync(
        (input, ctx) =>
        {
            var data = input.TryGetValue("input_data", out var d) && d is Dictionary<string, object?> dict ? dict : input;
            var count = data.TryGetValue("count", out var c) && c is int val ? val : 0;
            return new Dictionary<string, object?> { ["count"] = count + 1, ["iteration_at"] = DateTime.UtcNow.ToString("O") };
        },
        id: "increment",
        description: "Increment counter by 1");

    var loopFlow = new Flow(id: "counter_loop", description: "Increment counter until it reaches 5", storage: storage)
        .SetMetadata("category", "control-flow")
        .Loop(
            condition: data => data.TryGetValue("count", out var c) && c is int count && count < 5,
            task: incrementTask,
            maxIterations: 10);

    registry.Register(loopFlow);

    // 5. Error Handling Flow
    var riskyTask = new WaterTask(
        execute: (input, ctx) =>
        {
            var data = input.TryGetValue("input_data", out var d) && d is Dictionary<string, object?> dict ? dict : input;
            if (data.TryGetValue("fail", out var f) && f is bool shouldFail && shouldFail)
                throw new Exception("Simulated failure for testing");
            return Task.FromResult<Dictionary<string, object?>>(new() { ["status"] = "success", ["message"] = "Operation completed" });
        },
        id: "risky_operation",
        description: "Operation that may fail");

    var recoveryTask = TaskFactory.CreateSync(
        (input, ctx) =>
        {
            var data = input.TryGetValue("input_data", out var d) && d is Dictionary<string, object?> dict ? dict : input;
            var error = data.TryGetValue("_error", out var e) ? e?.ToString() : "unknown";
            return new Dictionary<string, object?> { ["status"] = "recovered", ["original_error"] = error, ["recovered_at"] = DateTime.UtcNow.ToString("O") };
        },
        id: "recovery",
        description: "Recover from failure");

    var errorFlow = new Flow(id: "error_handling", description: "Demonstrates try-catch error recovery", storage: storage)
        .SetMetadata("category", "resilience")
        .TryCatch(riskyTask, recoveryTask);

    registry.Register(errorFlow);

    return registry;
});

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
app.UseStaticFiles();
app.UseRouting();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

// Keep API endpoints
app.MapGet("/api/info", () => Results.Ok(new
{
    name = "Water Flows API",
    version = "1.0.0",
    status = "running"
}));

app.Run();
