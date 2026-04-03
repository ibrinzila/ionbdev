using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using PortWhisperer.Core.Models;

namespace PortWhisperer.Core.Services;

public class PortScanner
{
    private static readonly string[] ProjectMarkers =
        ["package.json", "Cargo.toml", "go.mod", "pyproject.toml", "Gemfile", "pom.xml", "build.gradle", "*.csproj", "*.sln"];

    public List<PortInfo> GetListeningPorts(bool detailed = false)
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            return GetListeningPortsWindows(detailed);

        return GetListeningPortsUnix(detailed);
    }

    private List<PortInfo> GetListeningPortsUnix(bool detailed)
    {
        var raw = ShellRunner.Run("lsof -iTCP -sTCP:LISTEN -P -n 2>/dev/null");
        if (string.IsNullOrEmpty(raw)) return [];

        var lines = raw.Split('\n', StringSplitOptions.RemoveEmptyEntries).Skip(1);
        var portMap = new Dictionary<int, bool>();
        var entries = new List<(int Port, int Pid, string ProcessName)>();

        foreach (var line in lines)
        {
            var parts = Regex.Split(line.Trim(), @"\s+");
            if (parts.Length < 9) continue;

            var processName = parts[0];
            if (!int.TryParse(parts[1], out var pid)) continue;
            var nameField = parts[8];

            var portMatch = Regex.Match(nameField, @":(\d+)$");
            if (!portMatch.Success) continue;
            var port = int.Parse(portMatch.Groups[1].Value);

            if (portMap.ContainsKey(port)) continue;
            portMap[port] = true;
            entries.Add((port, pid, processName));
        }

        var uniquePids = entries.Select(e => e.Pid).Distinct().ToList();
        var psMap = BatchPsInfo(uniquePids);
        var cwdMap = BatchCwd(uniquePids);
        var hasDocker = entries.Any(e => e.ProcessName.StartsWith("com.docke") || e.ProcessName == "docker");
        var dockerMap = hasDocker ? BatchDockerInfo() : new Dictionary<int, (string Name, string Image)>();

        return entries.Select(e => BuildPortInfo(e.Port, e.Pid, e.ProcessName, psMap, cwdMap, dockerMap, detailed))
                      .OrderBy(p => p.Port)
                      .ToList();
    }

    private List<PortInfo> GetListeningPortsWindows(bool detailed)
    {
        var raw = ShellRunner.Run("netstat -ano -p TCP | findstr LISTENING");
        if (string.IsNullOrEmpty(raw)) return [];

        var portMap = new Dictionary<int, bool>();
        var entries = new List<(int Port, int Pid, string ProcessName)>();

        foreach (var line in raw.Split('\n', StringSplitOptions.RemoveEmptyEntries))
        {
            var parts = Regex.Split(line.Trim(), @"\s+");
            if (parts.Length < 5) continue;

            var localAddr = parts[1];
            var portMatch = Regex.Match(localAddr, @":(\d+)$");
            if (!portMatch.Success) continue;
            var port = int.Parse(portMatch.Groups[1].Value);

            if (portMap.ContainsKey(port)) continue;
            if (!int.TryParse(parts[4], out var pid)) continue;

            portMap[port] = true;
            var procName = GetWindowsProcessName(pid);
            entries.Add((port, pid, procName));
        }

        var psMap = new Dictionary<int, PsInfo>();
        var cwdMap = new Dictionary<int, string>();
        var dockerMap = new Dictionary<int, (string Name, string Image)>();

        return entries.Select(e => BuildPortInfo(e.Port, e.Pid, e.ProcessName, psMap, cwdMap, dockerMap, detailed))
                      .OrderBy(p => p.Port)
                      .ToList();
    }

    private static string GetWindowsProcessName(int pid)
    {
        try
        {
            var proc = System.Diagnostics.Process.GetProcessById(pid);
            return proc.ProcessName;
        }
        catch { return "unknown"; }
    }

    private PortInfo BuildPortInfo(int port, int pid, string processName,
        Dictionary<int, PsInfo> psMap, Dictionary<int, string> cwdMap,
        Dictionary<int, (string Name, string Image)> dockerMap, bool detailed)
    {
        psMap.TryGetValue(pid, out var ps);
        cwdMap.TryGetValue(pid, out var cwd);

        var status = PortStatus.Healthy;
        string? memory = null;
        string? uptime = null;
        DateTime? startTime = null;
        string? framework = null;

        if (ps is not null)
        {
            if (ps.Stat.Contains('Z')) status = PortStatus.Zombie;
            else if (ps.Ppid == 1 && ProcessClassifier.IsDevProcess(processName, ps.Command))
                status = PortStatus.Orphaned;

            if (ps.RssKb > 0) memory = FormatMemory(ps.RssKb);

            if (ps.StartTime is { } st)
            {
                startTime = st;
                uptime = FormatUptime(DateTime.Now - st);
            }

            framework = FrameworkDetector.DetectFromCommand(ps.Command, processName);
        }

        string? projectName = null;
        string? cwdResult = null;
        string? gitBranch = null;

        // Docker container detection
        if (dockerMap.TryGetValue(port, out var docker))
        {
            projectName = docker.Name;
            framework = FrameworkDetector.DetectFromDockerImage(docker.Image);
            processName = "docker";
        }
        else if (cwd is not null)
        {
            var projectRoot = FindProjectRoot(cwd);
            cwdResult = projectRoot;
            projectName = Path.GetFileName(projectRoot);
            framework ??= FrameworkDetector.DetectFromProjectRoot(projectRoot);

            if (detailed)
            {
                gitBranch = ShellRunner.Run($"git -C \"{cwdResult}\" rev-parse --abbrev-ref HEAD 2>/dev/null");
            }
        }

        var processTree = detailed ? GetProcessTree(pid) : [];

        return new PortInfo
        {
            Port = port,
            Pid = pid,
            ProcessName = processName,
            RawName = processName,
            Command = ps?.Command ?? "",
            Cwd = cwdResult,
            ProjectName = projectName,
            Framework = framework,
            Uptime = uptime,
            StartTime = startTime,
            Status = status,
            Memory = memory,
            GitBranch = gitBranch,
            ProcessTree = processTree,
        };
    }

    public PortInfo? GetPortDetails(int targetPort)
    {
        return GetListeningPorts(detailed: true).FirstOrDefault(p => p.Port == targetPort);
    }

    public List<PortInfo> FindOrphanedProcesses()
    {
        return GetListeningPorts()
            .Where(p => p.Status is PortStatus.Orphaned or PortStatus.Zombie)
            .ToList();
    }

    public List<ProcessInfo> GetAllProcesses()
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            return GetAllProcessesWindows();

        return GetAllProcessesUnix();
    }

    private List<ProcessInfo> GetAllProcessesUnix()
    {
        var raw = ShellRunner.Run("ps -eo pid=,pcpu=,pmem=,rss=,lstart=,command= 2>/dev/null");
        if (string.IsNullOrEmpty(raw)) return [];

        var entries = new List<(int Pid, double Cpu, int Rss, string Lstart, string Command, string ProcessName)>();
        var seen = new HashSet<int>();
        var myPid = Environment.ProcessId;

        foreach (var line in raw.Split('\n', StringSplitOptions.RemoveEmptyEntries))
        {
            var m = Regex.Match(line.Trim(),
                @"^(\d+)\s+([\d.]+)\s+([\d.]+)\s+(\d+)\s+\w+\s+(\w+\s+\d+\s+[\d:]+\s+\d+)\s+(.*)$");
            if (!m.Success) continue;

            var pid = int.Parse(m.Groups[1].Value);
            if (pid <= 1 || pid == myPid || !seen.Add(pid)) continue;

            var command = m.Groups[6].Value;
            var procName = Path.GetFileName(command.Split(' ', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault() ?? "");

            entries.Add((pid, double.Parse(m.Groups[2].Value), int.Parse(m.Groups[4].Value),
                         m.Groups[5].Value, command, procName));
        }

        var nonDockerPids = entries
            .Where(e => !ProcessClassifier.IsDockerProcess(e.ProcessName))
            .Select(e => e.Pid).ToList();
        var cwdMap = BatchCwd(nonDockerPids);

        return entries.Select(e =>
        {
            cwdMap.TryGetValue(e.Pid, out var cwd);
            string? projectName = null;
            string? framework = FrameworkDetector.DetectFromCommand(e.Command, e.ProcessName);
            string? uptime = null;

            if (DateTime.TryParse(e.Lstart, out var startTime))
                uptime = FormatUptime(DateTime.Now - startTime);

            if (cwd is not null)
            {
                var root = FindProjectRoot(cwd);
                cwd = root;
                projectName = Path.GetFileName(root);
                framework ??= FrameworkDetector.DetectFromProjectRoot(root);
            }

            return new ProcessInfo
            {
                Pid = e.Pid,
                ProcessName = e.ProcessName,
                Command = e.Command,
                Description = SummarizeCommand(e.Command, e.ProcessName),
                Cpu = e.Cpu,
                Memory = e.Rss > 0 ? FormatMemory(e.Rss) : null,
                Cwd = cwd,
                ProjectName = projectName,
                Framework = framework,
                Uptime = uptime,
            };
        }).ToList();
    }

    private List<ProcessInfo> GetAllProcessesWindows()
    {
        var processes = System.Diagnostics.Process.GetProcesses();
        var myPid = Environment.ProcessId;

        return processes
            .Where(p => p.Id > 1 && p.Id != myPid)
            .Select(p =>
            {
                try
                {
                    return new ProcessInfo
                    {
                        Pid = p.Id,
                        ProcessName = p.ProcessName,
                        Command = p.ProcessName,
                        Description = p.MainWindowTitle.Length > 0 ? p.MainWindowTitle : p.ProcessName,
                        Cpu = 0,
                        Memory = FormatMemory((int)(p.WorkingSet64 / 1024)),
                        Framework = FrameworkDetector.DetectFromProcessName(p.ProcessName),
                    };
                }
                catch { return null; }
            })
            .Where(p => p is not null)
            .Cast<ProcessInfo>()
            .ToList();
    }

    public IDisposable WatchPorts(Action<WatchEvent> callback, int intervalMs = 2000)
    {
        var previousPorts = new HashSet<int>();

        void Check()
        {
            var current = GetListeningPorts();
            var currentSet = new HashSet<int>(current.Select(p => p.Port));

            foreach (var p in current)
            {
                if (!previousPorts.Contains(p.Port))
                    callback(new WatchEvent(WatchEventType.New, p, p.Port));
            }

            foreach (var port in previousPorts)
            {
                if (!currentSet.Contains(port))
                    callback(new WatchEvent(WatchEventType.Removed, null, port));
            }

            previousPorts = currentSet;
        }

        Check();
        var timer = new Timer(_ => Check(), null, intervalMs, intervalMs);
        return timer;
    }

    // --- Batch helpers (Unix) ---

    private record PsInfo(int Ppid, string Stat, int RssKb, DateTime? StartTime, string Command);

    private static Dictionary<int, PsInfo> BatchPsInfo(List<int> pids)
    {
        var map = new Dictionary<int, PsInfo>();
        if (pids.Count == 0) return map;

        var pidList = string.Join(",", pids);
        var raw = ShellRunner.Run($"ps -p {pidList} -o pid=,ppid=,stat=,rss=,lstart=,command= 2>/dev/null", 5000);
        if (string.IsNullOrEmpty(raw)) return map;

        foreach (var line in raw.Split('\n', StringSplitOptions.RemoveEmptyEntries))
        {
            var m = Regex.Match(line.Trim(),
                @"^(\d+)\s+(\d+)\s+(\S+)\s+(\d+)\s+\w+\s+(\w+\s+\d+\s+[\d:]+\s+\d+)\s+(.*)$");
            if (!m.Success) continue;

            DateTime? startTime = null;
            if (DateTime.TryParse(m.Groups[5].Value, out var st)) startTime = st;

            map[int.Parse(m.Groups[1].Value)] = new PsInfo(
                int.Parse(m.Groups[2].Value),
                m.Groups[3].Value,
                int.Parse(m.Groups[4].Value),
                startTime,
                m.Groups[6].Value
            );
        }

        return map;
    }

    private static Dictionary<int, string> BatchCwd(List<int> pids)
    {
        var map = new Dictionary<int, string>();
        if (pids.Count == 0) return map;

        if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
        {
            // On Linux, read /proc/<pid>/cwd symlinks
            foreach (var pid in pids)
            {
                try
                {
                    var link = $"/proc/{pid}/cwd";
                    if (File.Exists(link) || Directory.Exists(link))
                    {
                        var target = Path.GetFullPath(new FileInfo(link).LinkTarget ?? "");
                        if (!string.IsNullOrEmpty(target)) map[pid] = target;
                    }
                }
                catch { }
            }
            return map;
        }

        // macOS: use lsof
        var pidList = string.Join(",", pids);
        var raw = ShellRunner.Run($"lsof -a -d cwd -p {pidList} 2>/dev/null");
        if (string.IsNullOrEmpty(raw)) return map;

        foreach (var line in raw.Split('\n', StringSplitOptions.RemoveEmptyEntries).Skip(1))
        {
            var parts = Regex.Split(line.Trim(), @"\s+");
            if (parts.Length < 9) continue;
            if (!int.TryParse(parts[1], out var pid)) continue;
            var path = string.Join(" ", parts.Skip(8));
            if (path.StartsWith("/")) map[pid] = path;
        }

        return map;
    }

    private static Dictionary<int, (string Name, string Image)> BatchDockerInfo()
    {
        var map = new Dictionary<int, (string, string)>();
        var raw = ShellRunner.Run("docker ps --format \"{{.Ports}}\\t{{.Names}}\\t{{.Image}}\" 2>/dev/null", 5000);
        if (string.IsNullOrEmpty(raw)) return map;

        foreach (var line in raw.Split('\n', StringSplitOptions.RemoveEmptyEntries))
        {
            var parts = line.Split('\t');
            if (parts.Length < 3) continue;
            var (portsStr, name, image) = (parts[0], parts[1], parts[2]);

            var portMatches = Regex.Matches(portsStr, @"(?:\d+\.\d+\.\d+\.\d+|::):(\d+)->");
            var seen = new HashSet<int>();
            foreach (Match pm in portMatches)
            {
                var port = int.Parse(pm.Groups[1].Value);
                if (seen.Add(port)) map.TryAdd(port, (name, image));
            }
        }

        return map;
    }

    private static List<ProcessTreeNode> GetProcessTree(int pid)
    {
        var tree = new List<ProcessTreeNode>();
        var raw = ShellRunner.Run("ps -eo pid=,ppid=,comm= 2>/dev/null", 5000);
        if (string.IsNullOrEmpty(raw)) return tree;

        var processes = new Dictionary<int, (int Ppid, string Name)>();
        foreach (var line in raw.Split('\n', StringSplitOptions.RemoveEmptyEntries))
        {
            var parts = line.Trim().Split(' ', 3, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length < 3) continue;
            if (int.TryParse(parts[0], out var p) && int.TryParse(parts[1], out var pp))
                processes[p] = (pp, parts[2]);
        }

        var currentPid = pid;
        var depth = 0;
        while (currentPid > 1 && depth < 8)
        {
            if (!processes.TryGetValue(currentPid, out var proc)) break;
            tree.Add(new ProcessTreeNode(currentPid, proc.Ppid, proc.Name));
            currentPid = proc.Ppid;
            depth++;
        }

        return tree;
    }

    private static string FindProjectRoot(string dir)
    {
        var current = dir;
        var depth = 0;
        while (current != Path.GetPathRoot(current) && depth < 15)
        {
            foreach (var marker in ProjectMarkers)
            {
                if (marker.Contains('*'))
                {
                    if (Directory.Exists(current) &&
                        Directory.GetFiles(current, marker).Length > 0)
                        return current;
                }
                else if (File.Exists(Path.Combine(current, marker)))
                    return current;
            }
            var parent = Directory.GetParent(current)?.FullName;
            if (parent is null) break;
            current = parent;
            depth++;
        }
        return dir;
    }

    private static string SummarizeCommand(string command, string processName)
    {
        var parts = command.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var meaningful = new List<string>();

        for (var i = 0; i < parts.Length; i++)
        {
            if (i == 0) continue;
            if (parts[i].StartsWith('-')) continue;
            meaningful.Add(parts[i].Contains('/') ? Path.GetFileName(parts[i]) : parts[i]);
            if (meaningful.Count >= 3) break;
        }

        return meaningful.Count > 0 ? string.Join(" ", meaningful) : processName;
    }

    public static string FormatUptime(TimeSpan ts)
    {
        if (ts.TotalDays >= 1) return $"{(int)ts.TotalDays}d {ts.Hours}h";
        if (ts.TotalHours >= 1) return $"{(int)ts.TotalHours}h {ts.Minutes}m";
        if (ts.TotalMinutes >= 1) return $"{(int)ts.TotalMinutes}m {ts.Seconds}s";
        return $"{(int)ts.TotalSeconds}s";
    }

    public static string FormatMemory(long rssKb)
    {
        if (rssKb > 1_048_576) return $"{rssKb / 1_048_576.0:F1} GB";
        if (rssKb > 1024) return $"{rssKb / 1024.0:F1} MB";
        return $"{rssKb} KB";
    }
}
