using System.Text.Json;

namespace PortWhisperer.Core.Services;

public static class FrameworkDetector
{
    public static string? DetectFromProjectRoot(string projectRoot)
    {
        var pkgPath = Path.Combine(projectRoot, "package.json");
        if (File.Exists(pkgPath))
        {
            try
            {
                var json = JsonDocument.Parse(File.ReadAllText(pkgPath));
                var allDeps = new Dictionary<string, bool>();

                void CollectDeps(string section)
                {
                    if (json.RootElement.TryGetProperty(section, out var deps) &&
                        deps.ValueKind == JsonValueKind.Object)
                    {
                        foreach (var dep in deps.EnumerateObject())
                            allDeps.TryAdd(dep.Name, true);
                    }
                }

                CollectDeps("dependencies");
                CollectDeps("devDependencies");

                if (allDeps.ContainsKey("next")) return "Next.js";
                if (allDeps.ContainsKey("nuxt") || allDeps.ContainsKey("nuxt3")) return "Nuxt";
                if (allDeps.ContainsKey("@sveltejs/kit")) return "SvelteKit";
                if (allDeps.ContainsKey("svelte")) return "Svelte";
                if (allDeps.ContainsKey("@remix-run/react") || allDeps.ContainsKey("remix")) return "Remix";
                if (allDeps.ContainsKey("astro")) return "Astro";
                if (allDeps.ContainsKey("vite")) return "Vite";
                if (allDeps.ContainsKey("@angular/core")) return "Angular";
                if (allDeps.ContainsKey("vue")) return "Vue";
                if (allDeps.ContainsKey("react")) return "React";
                if (allDeps.ContainsKey("express")) return "Express";
                if (allDeps.ContainsKey("fastify")) return "Fastify";
                if (allDeps.ContainsKey("hono")) return "Hono";
                if (allDeps.ContainsKey("koa")) return "Koa";
                if (allDeps.ContainsKey("nestjs") || allDeps.ContainsKey("@nestjs/core")) return "NestJS";
                if (allDeps.ContainsKey("gatsby")) return "Gatsby";
                if (allDeps.ContainsKey("webpack-dev-server")) return "Webpack";
                if (allDeps.ContainsKey("esbuild")) return "esbuild";
                if (allDeps.ContainsKey("parcel")) return "Parcel";
            }
            catch { }
        }

        // Config file detection
        if (File.Exists(Path.Combine(projectRoot, "vite.config.ts")) ||
            File.Exists(Path.Combine(projectRoot, "vite.config.js"))) return "Vite";
        if (File.Exists(Path.Combine(projectRoot, "next.config.js")) ||
            File.Exists(Path.Combine(projectRoot, "next.config.mjs"))) return "Next.js";
        if (File.Exists(Path.Combine(projectRoot, "angular.json"))) return "Angular";
        if (File.Exists(Path.Combine(projectRoot, "Cargo.toml"))) return "Rust";
        if (File.Exists(Path.Combine(projectRoot, "go.mod"))) return "Go";
        if (File.Exists(Path.Combine(projectRoot, "manage.py"))) return "Django";
        if (File.Exists(Path.Combine(projectRoot, "Gemfile"))) return "Ruby";

        return null;
    }

    public static string? DetectFromCommand(string? command, string? processName)
    {
        if (string.IsNullOrEmpty(command)) return DetectFromProcessName(processName);
        var cmd = command.ToLowerInvariant();

        if (cmd.Contains("next")) return "Next.js";
        if (cmd.Contains("vite")) return "Vite";
        if (cmd.Contains("nuxt")) return "Nuxt";
        if (cmd.Contains("angular") || cmd.Contains("ng serve")) return "Angular";
        if (cmd.Contains("webpack")) return "Webpack";
        if (cmd.Contains("remix")) return "Remix";
        if (cmd.Contains("astro")) return "Astro";
        if (cmd.Contains("gatsby")) return "Gatsby";
        if (cmd.Contains("flask")) return "Flask";
        if (cmd.Contains("django") || cmd.Contains("manage.py")) return "Django";
        if (cmd.Contains("uvicorn")) return "FastAPI";
        if (cmd.Contains("rails")) return "Rails";
        if (cmd.Contains("cargo") || cmd.Contains("rustc")) return "Rust";
        if (cmd.Contains("dotnet")) return ".NET";

        return DetectFromProcessName(processName);
    }

    public static string? DetectFromProcessName(string? processName)
    {
        var name = (processName ?? "").ToLowerInvariant();
        return name switch
        {
            "node" => "Node.js",
            "python" or "python3" => "Python",
            "ruby" => "Ruby",
            "java" => "Java",
            "go" => "Go",
            "dotnet" => ".NET",
            _ => null,
        };
    }

    public static string DetectFromDockerImage(string? image)
    {
        if (string.IsNullOrEmpty(image)) return "Docker";
        var img = image.ToLowerInvariant();

        if (img.Contains("postgres")) return "PostgreSQL";
        if (img.Contains("redis")) return "Redis";
        if (img.Contains("mysql") || img.Contains("mariadb")) return "MySQL";
        if (img.Contains("mongo")) return "MongoDB";
        if (img.Contains("nginx")) return "nginx";
        if (img.Contains("localstack")) return "LocalStack";
        if (img.Contains("rabbitmq")) return "RabbitMQ";
        if (img.Contains("kafka")) return "Kafka";
        if (img.Contains("elasticsearch") || img.Contains("opensearch")) return "Elasticsearch";
        if (img.Contains("minio")) return "MinIO";
        return "Docker";
    }
}
