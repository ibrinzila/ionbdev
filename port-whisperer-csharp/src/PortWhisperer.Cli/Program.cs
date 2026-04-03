using PortWhisperer.Core.Models;
using PortWhisperer.Core.Services;
using Spectre.Console;

namespace PortWhisperer.Cli;

public static class Program
{
    public static async Task<int> Main(string[] args)
    {
        var showAll = args.Contains("--all") || args.Contains("-a");
        var filteredArgs = args.Where(a => a is not "--all" and not "-a").ToArray();
        var command = filteredArgs.FirstOrDefault();

        var scanner = new PortScanner();

        try
        {
            if (command is null)
                return RunPortList(scanner, showAll);

            if (int.TryParse(command, out var portNum))
                return await RunPortInspect(scanner, portNum);

            return command switch
            {
                "ps" => RunProcessList(scanner, showAll),
                "clean" => await RunClean(scanner),
                "watch" => RunWatch(scanner),
                "help" or "--help" or "-h" => RunHelp(),
                _ => RunUnknown(command),
            };
        }
        catch (Exception ex)
        {
            AnsiConsole.MarkupLine($"\n  [red]Error: {Markup.Escape(ex.Message)}[/]\n");
            return 1;
        }
    }

    private static int RunPortList(PortScanner scanner, bool showAll)
    {
        var ports = scanner.GetListeningPorts();
        if (!showAll)
            ports = ports.Where(p => ProcessClassifier.IsDevProcess(p.ProcessName, p.Command)).ToList();
        ConsoleDisplay.DisplayPortTable(ports, !showAll);
        return 0;
    }

    private static async Task<int> RunPortInspect(PortScanner scanner, int portNum)
    {
        var info = scanner.GetPortDetails(portNum);
        ConsoleDisplay.DisplayPortDetail(info);

        if (info is not null)
        {
            if (AnsiConsole.Confirm($"  [yellow]Kill process on :{portNum}?[/]", defaultValue: false))
            {
                var success = ShellRunner.KillProcess(info.Pid);
                if (success)
                    AnsiConsole.MarkupLine($"\n  [green]✓ Killed PID {info.Pid}[/]\n");
                else
                    AnsiConsole.MarkupLine($"\n  [red]✕ Failed. Try: sudo kill -9 {info.Pid}[/]\n");
            }
        }

        return 0;
    }

    private static int RunProcessList(PortScanner scanner, bool showAll)
    {
        var processes = scanner.GetAllProcesses();
        if (!showAll)
        {
            processes = processes.Where(p => ProcessClassifier.IsDevProcess(p.ProcessName, p.Command)).ToList();

            // Collapse Docker processes into one summary row
            var dockerProcs = processes.Where(p => ProcessClassifier.IsDockerProcess(p.ProcessName)).ToList();
            var nonDocker = processes.Where(p => !ProcessClassifier.IsDockerProcess(p.ProcessName)).ToList();

            if (dockerProcs.Count > 0)
            {
                var totalCpu = dockerProcs.Sum(p => p.Cpu);
                var totalRssKb = dockerProcs.Sum(p => ParseMemoryToKb(p.Memory));

                nonDocker.Add(new ProcessInfo
                {
                    Pid = dockerProcs[0].Pid,
                    ProcessName = "Docker",
                    Command = "",
                    Description = $"{dockerProcs.Count} processes",
                    Cpu = totalCpu,
                    Memory = PortScanner.FormatMemory((long)totalRssKb),
                    Framework = "Docker",
                    Uptime = dockerProcs[0].Uptime,
                });
            }

            processes = nonDocker;
        }

        processes = processes.OrderByDescending(p => p.Cpu).ToList();
        ConsoleDisplay.DisplayProcessTable(processes, !showAll);
        return 0;
    }

    private static async Task<int> RunClean(PortScanner scanner)
    {
        var orphaned = scanner.FindOrphanedProcesses();
        var killed = new List<int>();
        var failed = new List<int>();

        if (orphaned.Count == 0)
        {
            ConsoleDisplay.DisplayCleanResults(orphaned, killed, failed);
            return 0;
        }

        AnsiConsole.WriteLine();
        AnsiConsole.MarkupLine(
            $"  [yellow bold]Found {orphaned.Count} orphaned/zombie process{(orphaned.Count == 1 ? "" : "es")}:[/]");
        foreach (var p in orphaned)
        {
            AnsiConsole.MarkupLine(
                $"  [grey]•[/] [white bold]:{p.Port}[/] — {Markup.Escape(p.ProcessName)} [grey](PID {p.Pid})[/]");
        }
        AnsiConsole.WriteLine();

        if (AnsiConsole.Confirm("  [yellow]Kill all?[/]", defaultValue: false))
        {
            foreach (var p in orphaned)
            {
                if (ShellRunner.KillProcess(p.Pid))
                    killed.Add(p.Pid);
                else
                    failed.Add(p.Pid);
            }
            ConsoleDisplay.DisplayCleanResults(orphaned, killed, failed);
        }
        else
        {
            AnsiConsole.MarkupLine("\n  [grey]Aborted.[/]\n");
        }

        return 0;
    }

    private static int RunWatch(PortScanner scanner)
    {
        ConsoleDisplay.DisplayWatchHeader();

        using var cts = new CancellationTokenSource();
        Console.CancelKeyPress += (_, e) =>
        {
            e.Cancel = true;
            cts.Cancel();
        };

        using var watcher = scanner.WatchPorts(evt => ConsoleDisplay.DisplayWatchEvent(evt), 2000);

        try
        {
            Task.Delay(Timeout.Infinite, cts.Token).Wait();
        }
        catch (AggregateException) { }

        AnsiConsole.MarkupLine("\n\n  [grey]Stopped watching.[/]\n");
        return 0;
    }

    private static int RunHelp()
    {
        ConsoleDisplay.DisplayHelp();
        return 0;
    }

    private static int RunUnknown(string command)
    {
        AnsiConsole.MarkupLine($"\n  [red]Unknown command: {Markup.Escape(command)}[/]");
        AnsiConsole.MarkupLine($"  [grey]Run[/] [cyan1]ports --help[/] [grey]for usage.[/]\n");
        return 1;
    }

    private static double ParseMemoryToKb(string? memory)
    {
        if (string.IsNullOrEmpty(memory)) return 0;
        var parts = memory.Split(' ');
        if (parts.Length < 2 || !double.TryParse(parts[0], out var val)) return 0;
        return parts[1] switch
        {
            "GB" => val * 1_048_576,
            "MB" => val * 1024,
            _ => val,
        };
    }
}
