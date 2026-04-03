using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using PortWhisperer.Desktop.Views;

namespace PortWhisperer.Desktop;

public class App : Application
{
    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            desktop.MainWindow = new MainWindow();
            desktop.ShutdownMode = ShutdownMode.OnExplicitShutdown;

            // Create system tray icon
            var trayIcon = new TrayIcon
            {
                ToolTipText = "Port Whisperer",
                IsVisible = true,
                Menu = CreateTrayMenu(desktop),
            };

            trayIcon.Clicked += (_, _) => ToggleMainWindow(desktop);

            // Set up tray icons collection
            TrayIcon.SetIcons(this, [trayIcon]);

            // Start minimized to tray
            if (desktop.MainWindow is not null)
                desktop.MainWindow.ShowInTaskbar = true;
        }

        base.OnFrameworkInitializationCompleted();
    }

    private static NativeMenu CreateTrayMenu(IClassicDesktopStyleApplicationLifetime desktop)
    {
        var menu = new NativeMenu();

        var showItem = new NativeMenuItem("Show Port Whisperer");
        showItem.Click += (_, _) => ToggleMainWindow(desktop);
        menu.Add(showItem);

        menu.Add(new NativeMenuItemSeparator());

        var refreshItem = new NativeMenuItem("Refresh Ports");
        refreshItem.Click += (_, _) =>
        {
            if (desktop.MainWindow is MainWindow mw)
                mw.RefreshPorts();
        };
        menu.Add(refreshItem);

        menu.Add(new NativeMenuItemSeparator());

        var quitItem = new NativeMenuItem("Quit");
        quitItem.Click += (_, _) => desktop.Shutdown();
        menu.Add(quitItem);

        return menu;
    }

    private static void ToggleMainWindow(IClassicDesktopStyleApplicationLifetime desktop)
    {
        if (desktop.MainWindow is null) return;

        if (desktop.MainWindow.IsVisible)
        {
            desktop.MainWindow.Hide();
        }
        else
        {
            desktop.MainWindow.Show();
            desktop.MainWindow.Activate();
        }
    }
}
