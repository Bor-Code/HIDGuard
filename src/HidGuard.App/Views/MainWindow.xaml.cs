using System.ComponentModel;
using System.Windows;
using HidGuard.App.ViewModels;

namespace HidGuard.App.Views;

public partial class MainWindow : Window
{
    private bool allowClosing;

    public MainWindow(MainViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel ?? throw new ArgumentNullException(nameof(viewModel));
    }

    public void ShowAndActivate()
    {
        if (WindowState == WindowState.Minimized)
        {
            WindowState = WindowState.Normal;
        }

        Show();
        Activate();
    }

    public void ForceClose()
    {
        allowClosing = true;
        Close();
    }

    protected override void OnClosing(CancelEventArgs e)
    {
        ArgumentNullException.ThrowIfNull(e);

        if (!allowClosing)
        {
            e.Cancel = true;
            Hide();
            return;
        }

        base.OnClosing(e);
    }
}
