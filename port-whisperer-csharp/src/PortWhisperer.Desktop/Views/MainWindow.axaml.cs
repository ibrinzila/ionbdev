using Avalonia.Controls;
using PortWhisperer.Desktop.ViewModels;

namespace PortWhisperer.Desktop.Views;

public partial class MainWindow : Window
{
    private readonly MainWindowViewModel _viewModel;

    public MainWindow()
    {
        InitializeComponent();
        _viewModel = new MainWindowViewModel();
        DataContext = _viewModel;

        // Hide to tray instead of closing
        Closing += (_, e) =>
        {
            e.Cancel = true;
            Hide();
        };
    }

    public void RefreshPorts() => _viewModel.Refresh();
}
