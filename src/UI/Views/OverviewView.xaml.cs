using System.Windows;
using PingArmor.Models;
using PingArmor.Services;
using PingArmor.UI.ViewModels;

namespace PingArmor.UI.Views;

public partial class OverviewView : System.Windows.Controls.UserControl
{
    private readonly OverviewViewModel? _viewModel;
    private NetworkMonitor? _monitor;

    public OverviewView()
    {
        InitializeComponent();

        if (AppServices.IsReady)
        {
            _monitor = AppServices.Monitor!;
            _viewModel = new OverviewViewModel(AppServices.Config!, _monitor, AppServices.LogAppender);
            DataContext = _viewModel;
        }

        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
    }

    /// <summary>Applies the latest optimization plan (forwarded from the dashboard window).</summary>
    public void Update(OptimizationPlan? plan) => _viewModel?.Update(plan);

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        if (_monitor is not null)
        {
            _monitor.OptimizationApplied -= OnOptimizationApplied;
            _monitor.OptimizationApplied += OnOptimizationApplied;
        }

        _viewModel?.Update(null);
    }

    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        if (_monitor is not null)
        {
            _monitor.OptimizationApplied -= OnOptimizationApplied;
        }
    }

    private void OnOptimizationApplied(OptimizationResult result)
    {
        if (!Dispatcher.CheckAccess())
        {
            Dispatcher.InvokeAsync(() => _viewModel?.SetLastResult(result));
        }
        else
        {
            _viewModel?.SetLastResult(result);
        }
    }
}