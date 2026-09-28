using PingArmor.UI.ViewModels;

namespace PingArmor.UI.Views;

public partial class SettingsView : System.Windows.Controls.UserControl
{
    private readonly SettingsViewModel? _viewModel;

    public SettingsView()
    {
        InitializeComponent();

        if (AppServices.IsReady)
        {
            _viewModel = new SettingsViewModel(
                AppServices.Config!,
                AppServices.Engine!,
                AppServices.Monitor!,
                AppServices.LogAppender);
            DataContext = _viewModel;
            _viewModel.Sync();
        }
    }

    /// <summary>Re-reads the persisted settings into the bindings.</summary>
    public void Sync() => _viewModel?.Sync();
}