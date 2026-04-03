using System.Collections.ObjectModel;
using System.Reactive;
using System.Reactive.Linq;
using PortWhisperer.Core.Models;
using PortWhisperer.Core.Services;
using ReactiveUI;

namespace PortWhisperer.Desktop.ViewModels;

public class MainWindowViewModel : ReactiveObject
{
    private readonly PortScanner _scanner = new();
    private ObservableCollection<PortRowViewModel> _ports = [];
    private bool _showAll;
    private bool _isLoading;
    private string _statusText = "Ready";
    private IDisposable? _watcher;
    private bool _isWatching;

    public MainWindowViewModel()
    {
        RefreshCommand = ReactiveCommand.Create(Refresh);
        ToggleAllCommand = ReactiveCommand.Create(() =>
        {
            ShowAll = !ShowAll;
            Refresh();
        });
        KillProcessCommand = ReactiveCommand.Create<int>(KillProcess);
        ToggleWatchCommand = ReactiveCommand.Create(ToggleWatch);

        // Initial load
        Refresh();
    }

    public ObservableCollection<PortRowViewModel> Ports
    {
        get => _ports;
        set => this.RaiseAndSetIfChanged(ref _ports, value);
    }

    public bool ShowAll
    {
        get => _showAll;
        set => this.RaiseAndSetIfChanged(ref _showAll, value);
    }

    public bool IsLoading
    {
        get => _isLoading;
        set => this.RaiseAndSetIfChanged(ref _isLoading, value);
    }

    public string StatusText
    {
        get => _statusText;
        set => this.RaiseAndSetIfChanged(ref _statusText, value);
    }

    public bool IsWatching
    {
        get => _isWatching;
        set => this.RaiseAndSetIfChanged(ref _isWatching, value);
    }

    public ReactiveCommand<Unit, Unit> RefreshCommand { get; }
    public ReactiveCommand<Unit, Unit> ToggleAllCommand { get; }
    public ReactiveCommand<int, Unit> KillProcessCommand { get; }
    public ReactiveCommand<Unit, Unit> ToggleWatchCommand { get; }

    public void Refresh()
    {
        IsLoading = true;

        try
        {
            var ports = _scanner.GetListeningPorts();
            if (!ShowAll)
                ports = ports.Where(p => ProcessClassifier.IsDevProcess(p.ProcessName, p.Command)).ToList();

            Ports = new ObservableCollection<PortRowViewModel>(
                ports.Select(p => new PortRowViewModel(p)));

            StatusText = $"{ports.Count} port{(ports.Count == 1 ? "" : "s")} active";
        }
        catch (Exception ex)
        {
            StatusText = $"Error: {ex.Message}";
        }
        finally
        {
            IsLoading = false;
        }
    }

    private void KillProcess(int pid)
    {
        var success = ShellRunner.KillProcess(pid);
        StatusText = success ? $"Killed PID {pid}" : $"Failed to kill PID {pid}";
        Refresh();
    }

    private void ToggleWatch()
    {
        if (IsWatching)
        {
            _watcher?.Dispose();
            _watcher = null;
            IsWatching = false;
            StatusText = "Watch stopped";
        }
        else
        {
            _watcher = _scanner.WatchPorts(evt =>
            {
                // Marshal back to UI thread
                Avalonia.Threading.Dispatcher.UIThread.InvokeAsync(Refresh);
            }, 2000);
            IsWatching = true;
            StatusText = "Watching for changes...";
        }
    }
}

public class PortRowViewModel : ReactiveObject
{
    public PortRowViewModel(PortInfo info)
    {
        Port = info.Port;
        Pid = info.Pid;
        ProcessName = info.ProcessName;
        ProjectName = info.ProjectName ?? "-";
        Framework = info.Framework ?? "-";
        Uptime = info.Uptime ?? "-";
        Memory = info.Memory ?? "-";
        Status = info.Status;
        StatusDisplay = info.Status switch
        {
            PortStatus.Healthy => "● healthy",
            PortStatus.Orphaned => "● orphaned",
            PortStatus.Zombie => "● zombie",
            _ => "● unknown",
        };
        StatusColor = info.Status switch
        {
            PortStatus.Healthy => "#22c55e",
            PortStatus.Orphaned => "#eab308",
            PortStatus.Zombie => "#ef4444",
            _ => "#6b7280",
        };
    }

    public int Port { get; }
    public int Pid { get; }
    public string ProcessName { get; }
    public string ProjectName { get; }
    public string Framework { get; }
    public string Uptime { get; }
    public string Memory { get; }
    public PortStatus Status { get; }
    public string StatusDisplay { get; }
    public string StatusColor { get; }
}
