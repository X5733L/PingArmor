using System.Windows;
using PingArmor.UI.ViewModels;

namespace PingArmor.UI.Views;

public partial class AdaptersView : System.Windows.Controls.UserControl
{
    private readonly AdaptersViewModel? _viewModel;

    public AdaptersView()
    {
        InitializeComponent();

        if (AppServices.IsReady)
        {
            _viewModel = new AdaptersViewModel(
                AppServices.Engine!,
                AppServices.Config!,
                AppServices.Monitor!,
                AppServices.LogAppender);
            DataContext = _viewModel;
        }

        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
    }

    /// <summary>Reloads the adapter list (called when the page becomes visible).</summary>
    public void Refresh() => _viewModel?.RefreshCommand.Execute(null);

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        if (_viewModel is { HasAdapters: false })
        {
            Refresh();
        }
    }

    private void OnUnloaded(object sender, RoutedEventArgs e) => _viewModel?.Dispose();
}