using PortWhisperer.Core.Models;
using Spectre.Console;

namespace PortWhisperer.Cli;

public static class ConsoleDisplay
{
    private static readonly Dictionary<string, string> FrameworkColors = new()
    {
        ["Next.js"] = "white",
        ["Vite"] = "yellow",
        ["React"] = "cyan1",
        ["Vue"] = "green",
        ["Angular"] = "red",
        ["Svelte"] = "orangered1",
        ["SvelteKit"] = "orangered1",
        ["Express"] = "grey",
        ["Fastify"] = "white",
        ["NestJS"] = "red",
        ["Nuxt"] = "green",
        ["Remix"] = "blue",
        ["Astro"] = "magenta1",
        ["Django"] = "green",
        ["Flask"] = "white",
        ["FastAPI"] = "cyan1",
        ["Rails"] = "red",
        ["Gatsby"] = "magenta1",
        ["Go"] = "cyan1",
        ["Rust"] = "rgb(222,165,93)",
        ["Ruby"] = "red",
        ["Python"] = "yellow",
        ["Node.js"] = "green",
        [".NET"] = "magenta1",
        ["Java"] = "red",
        ["Hono"] = "rgb(255,102,0)",
        ["Koa"] = "white",
        ["Webpack"] = "blue",
        ["esbuild"] = "yellow",
        ["Parcel"] = "rgb(224,178,77)",
        ["Docker"] = "blue",
        ["PostgreSQL"] = "blue",
        ["Redis"] = "red",
        ["MySQL"] = "blue",
        ["MongoDB"] = "green",
        ["nginx"] = "green",
        ["LocalStack"] = "white",
        ["RabbitMQ"] = "rgb(255,102,0)",
        ["Kafka"] = "white",
        ["Elasticsearch"] = "yellow",
        ["MinIO"] = "red",
    };

    public static void RenderHeader()
    {
        AnsiConsole.WriteLine();
        var panel = new Panel(
            new Markup("[white bold]Port Whisperer[/]\n[grey]listening to your ports...[/]"))
        {
            Border = BoxBorder.Rounded,
            BorderStyle = new Style(Color.Cyan1),
            Padding = new Padding(2, 0, 2, 0),
        };
        AnsiConsole.Write(panel);
        AnsiConsole.WriteLine();
    }

    private static string FormatFramework(string? framework)
    {
        if (string.IsNullOrEmpty(framework)) return "[grey]-[/]";
        var color = FrameworkColors.GetValueOrDefault(framework, "white");
        return $"[{color}]{Markup.Escape(framework)}[/]";
    }

    private static string FormatStatus(PortStatus status) => status switch
    {
        PortStatus.Healthy => "[green]● healthy[/]",
        PortStatus.Orphaned => "[yellow]● orphaned[/]",
        PortStatus.Zombie => "[red]● zombie[/]",
        _ => "[grey]● unknown[/]",
    };

    public static void DisplayPortTable(List<PortInfo> ports, bool filtered)
    {
        RenderHeader();

        if (ports.Count == 0)
        {
            AnsiConsole.MarkupLine("  [grey]No active listening ports found.[/]");
            AnsiConsole.MarkupLine("  [grey]Start a dev server and run[/] [cyan1]ports[/] [grey]again.[/]");
            AnsiConsole.WriteLine();
            return;
        }

        var table = new Table()
            .Border(TableBorder.Rounded)
            .BorderColor(Color.Grey)
            .AddColumn(new TableColumn("[cyan1 bold]PORT[/]"))
            .AddColumn(new TableColumn("[cyan1 bold]PROCESS[/]"))
            .AddColumn(new TableColumn("[cyan1 bold]PID[/]"))
            .AddColumn(new TableColumn("[cyan1 bold]PROJECT[/]"))
            .AddColumn(new TableColumn("[cyan1 bold]FRAMEWORK[/]"))
            .AddColumn(new TableColumn("[cyan1 bold]UPTIME[/]"))
            .AddColumn(new TableColumn("[cyan1 bold]STATUS[/]"));

        foreach (var p in ports)
        {
            table.AddRow(
                $"[white bold]:{p.Port}[/]",
                $"[white]{Markup.Escape(p.ProcessName)}[/]",
                $"[grey]{p.Pid}[/]",
                p.ProjectName is not null
                    ? $"[blue]{Markup.Escape(Truncate(p.ProjectName, 20))}[/]"
                    : "[grey]-[/]",
                FormatFramework(p.Framework),
                p.Uptime is not null ? $"[yellow]{Markup.Escape(p.Uptime)}[/]" : "[grey]-[/]",
                FormatStatus(p.Status)
            );
        }

        AnsiConsole.Write(table);
        AnsiConsole.WriteLine();

        var hint = filtered ? "  [grey]·[/]  [cyan1]--all[/] [grey]to show everything[/]" : "";
        AnsiConsole.MarkupLine(
            $"  [grey]{ports.Count} port{(ports.Count == 1 ? "" : "s")} active  ·[/]  " +
            $"[grey]Run[/] [cyan1]ports <number>[/] [grey]for details[/]{hint}");
        AnsiConsole.WriteLine();
    }

    public static void DisplayProcessTable(List<ProcessInfo> processes, bool filtered)
    {
        RenderHeader();

        if (processes.Count == 0)
        {
            AnsiConsole.MarkupLine("  [grey]No dev processes found.[/]");
            AnsiConsole.MarkupLine("  [grey]Run[/] [cyan1]ports ps --all[/] [grey]to show all processes.[/]");
            AnsiConsole.WriteLine();
            return;
        }

        var table = new Table()
            .Border(TableBorder.Rounded)
            .BorderColor(Color.Grey)
            .AddColumn(new TableColumn("[cyan1 bold]PID[/]"))
            .AddColumn(new TableColumn("[cyan1 bold]PROCESS[/]"))
            .AddColumn(new TableColumn("[cyan1 bold]CPU%[/]"))
            .AddColumn(new TableColumn("[cyan1 bold]MEM[/]"))
            .AddColumn(new TableColumn("[cyan1 bold]PROJECT[/]"))
            .AddColumn(new TableColumn("[cyan1 bold]FRAMEWORK[/]"))
            .AddColumn(new TableColumn("[cyan1 bold]UPTIME[/]"))
            .AddColumn(new TableColumn("[cyan1 bold]WHAT[/]"));

        foreach (var p in processes)
        {
            var cpuStr = p.Cpu.ToString("F1");
            var cpuColor = p.Cpu > 25 ? "red" : p.Cpu > 5 ? "yellow" : "green";

            table.AddRow(
                $"[grey]{p.Pid}[/]",
                $"[white bold]{Markup.Escape(Truncate(p.ProcessName, 15))}[/]",
                $"[{cpuColor}]{cpuStr}[/]",
                p.Memory is not null ? $"[green]{Markup.Escape(p.Memory)}[/]" : "[grey]-[/]",
                p.ProjectName is not null
                    ? $"[blue]{Markup.Escape(Truncate(p.ProjectName, 20))}[/]"
                    : "[grey]-[/]",
                FormatFramework(p.Framework),
                p.Uptime is not null ? $"[yellow]{Markup.Escape(p.Uptime)}[/]" : "[grey]-[/]",
                $"[grey]{Markup.Escape(Truncate(p.Description ?? p.ProcessName, 30))}[/]"
            );
        }

        AnsiConsole.Write(table);
        AnsiConsole.WriteLine();

        var hint = filtered ? "  [grey]·[/]  [cyan1]--all[/] [grey]to show everything[/]" : "";
        AnsiConsole.MarkupLine($"  [grey]{processes.Count} process{(processes.Count == 1 ? "" : "es")}[/]{hint}");
        AnsiConsole.WriteLine();
    }

    public static void DisplayPortDetail(PortInfo? info)
    {
        RenderHeader();

        if (info is null)
        {
            AnsiConsole.MarkupLine("  [red]No process found on that port.[/]");
            AnsiConsole.WriteLine();
            return;
        }

        AnsiConsole.MarkupLine($"  [white bold]Port :{info.Port}[/]");
        AnsiConsole.MarkupLine($"  [grey]{new string('─', 40)}[/]");
        AnsiConsole.WriteLine();

        void Box(string label, string value) =>
            AnsiConsole.MarkupLine($"  [grey]{label,-16}[/] {value}");

        Box("Process", $"[white bold]{Markup.Escape(info.ProcessName)}[/]");
        Box("PID", $"[grey]{info.Pid}[/]");
        Box("Status", FormatStatus(info.Status));
        Box("Framework", FormatFramework(info.Framework));
        Box("Memory", info.Memory is not null ? $"[green]{Markup.Escape(info.Memory)}[/]" : "[grey]-[/]");
        Box("Uptime", info.Uptime is not null ? $"[yellow]{Markup.Escape(info.Uptime)}[/]" : "[grey]-[/]");
        if (info.StartTime.HasValue)
            Box("Started", $"[grey]{info.StartTime.Value:g}[/]");

        AnsiConsole.WriteLine();
        AnsiConsole.MarkupLine("  [cyan1 bold]Location[/]");
        AnsiConsole.MarkupLine($"  [grey]{new string('─', 40)}[/]");
        Box("Directory", info.Cwd is not null ? $"[blue]{Markup.Escape(info.Cwd)}[/]" : "[grey]-[/]");
        Box("Project", info.ProjectName is not null ? $"[white]{Markup.Escape(info.ProjectName)}[/]" : "[grey]-[/]");
        Box("Git Branch", info.GitBranch is not null ? $"[magenta1]{Markup.Escape(info.GitBranch)}[/]" : "[grey]-[/]");

        if (info.ProcessTree.Count > 0)
        {
            AnsiConsole.WriteLine();
            AnsiConsole.MarkupLine("  [cyan1 bold]Process Tree[/]");
            AnsiConsole.MarkupLine($"  [grey]{new string('─', 40)}[/]");

            for (var i = 0; i < info.ProcessTree.Count; i++)
            {
                var node = info.ProcessTree[i];
                var indent = new string(' ', i * 2);
                var prefix = i == 0 ? "→" : "└─";
                var pidColor = node.Pid == info.Pid ? "white bold" : "grey";
                AnsiConsole.MarkupLine(
                    $"  {indent}[grey]{prefix}[/] [{pidColor}]{Markup.Escape(node.Name)}[/] [grey]({node.Pid})[/]");
            }
        }

        AnsiConsole.WriteLine();
        AnsiConsole.MarkupLine($"  [grey]Kill this process:[/] [cyan1]ports clean[/] [grey]or[/] [red]kill {info.Pid}[/]");
        AnsiConsole.WriteLine();
    }

    public static void DisplayCleanResults(List<PortInfo> orphaned, List<int> killed, List<int> failed)
    {
        RenderHeader();

        if (orphaned.Count == 0)
        {
            AnsiConsole.MarkupLine("  [green]✓ No orphaned or zombie processes found. All clean![/]");
            AnsiConsole.WriteLine();
            return;
        }

        AnsiConsole.MarkupLine(
            $"  [yellow bold]Found {orphaned.Count} orphaned/zombie process{(orphaned.Count == 1 ? "" : "es")}:[/]");
        AnsiConsole.WriteLine();

        foreach (var p in orphaned)
        {
            var wasKilled = killed.Contains(p.Pid);
            var didFail = failed.Contains(p.Pid);
            var icon = wasKilled ? "[green]✓[/]" : didFail ? "[red]✕[/]" : "[yellow]?[/]";
            AnsiConsole.MarkupLine(
                $"  {icon} [white bold]:{p.Port}[/] [grey]—[/] {Markup.Escape(p.ProcessName)} [grey](PID {p.Pid})[/]");
            if (didFail)
                AnsiConsole.MarkupLine($"    [red]Failed to kill. Try: sudo kill -9 {p.Pid}[/]");
        }

        AnsiConsole.WriteLine();
        if (killed.Count > 0)
            AnsiConsole.MarkupLine($"  [green]Cleaned {killed.Count} process{(killed.Count == 1 ? "" : "es")}.[/]");
        if (failed.Count > 0)
            AnsiConsole.MarkupLine($"  [red]Failed to clean {failed.Count} process{(failed.Count == 1 ? "" : "es")}.[/]");
        AnsiConsole.WriteLine();
    }

    public static void DisplayWatchHeader()
    {
        RenderHeader();
        AnsiConsole.MarkupLine("  [cyan1 bold]Watching for port changes...[/]");
        AnsiConsole.MarkupLine("  [grey]Press Ctrl+C to stop[/]");
        AnsiConsole.WriteLine();
    }

    public static void DisplayWatchEvent(WatchEvent evt)
    {
        var timestamp = $"[grey]{DateTime.Now:T}[/]";

        if (evt.Type == WatchEventType.New && evt.Info is not null)
        {
            var fw = evt.Info.Framework is not null ? $" {FormatFramework(evt.Info.Framework)}" : "";
            var proj = evt.Info.ProjectName is not null ? $" [blue]\\[{Markup.Escape(evt.Info.ProjectName)}\\][/]" : "";
            AnsiConsole.MarkupLine(
                $"  {timestamp} [green]▲ NEW[/]    [white bold]:{evt.Info.Port}[/] ← [white]{Markup.Escape(evt.Info.ProcessName)}[/]{proj}{fw}");
        }
        else if (evt.Type == WatchEventType.Removed && evt.Port.HasValue)
        {
            AnsiConsole.MarkupLine(
                $"  {timestamp} [red]▼ CLOSED[/] [white bold]:{evt.Port}[/]");
        }
    }

    public static void DisplayHelp()
    {
        AnsiConsole.WriteLine();
        AnsiConsole.MarkupLine("  [cyan1 bold]Port Whisperer[/] [grey]— listen to your ports[/]");
        AnsiConsole.WriteLine();
        AnsiConsole.MarkupLine("  [white]Usage:[/]");
        AnsiConsole.MarkupLine("    [cyan1]ports[/]              Show dev server ports");
        AnsiConsole.MarkupLine("    [cyan1]ports --all[/]        Show all listening ports");
        AnsiConsole.MarkupLine("    [cyan1]ports ps[/]           Show all running dev processes");
        AnsiConsole.MarkupLine("    [cyan1]ports <number>[/]     Detailed info about a specific port");
        AnsiConsole.MarkupLine("    [cyan1]ports clean[/]        Kill orphaned/zombie dev servers");
        AnsiConsole.MarkupLine("    [cyan1]ports watch[/]        Monitor port changes in real-time");
        AnsiConsole.WriteLine();
    }

    private static string Truncate(string str, int max)
    {
        if (string.IsNullOrEmpty(str)) return "";
        return str.Length > max ? str[..(max - 1)] + "…" : str;
    }
}
