using System.Collections.Specialized;
using System.Windows;
using PingArmor.Services;
using PingArmor.UI.ViewModels;

namespace PingArmor.UI.Views;

public partial class LogView : System.Windows.Controls.UserControl
{
    private readonly LogViewModel? _viewModel;

    public LogView()
    {
        InitializeComponent();

        if (AppServices.IsReady)
        {
            _viewModel = new LogViewModel(AppServices.Monitor!, AppServices.LogAppender);
            DataContext = _viewModel;
            _viewModel.Entries.CollectionChanged += OnEntriesChanged;
        }

        LogService.EntryAppended += OnEntryAppended;
        Unloaded += OnUnloaded;
    }

    private void OnEntryAppended(LogEntry entry)
    {
        if (!Dispatcher.CheckAccess())
        {
            Dispatcher.InvokeAsync(() => _viewModel?.AppendEntry(entry));
        }
        else
        {
            _viewModel?.AppendEntry(entry);
        }
    }

    private void OnEntriesChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (e.Action != NotifyCollectionChangedAction.Add) return;
        if (_viewModel?.AutoScroll != true) return;
        if (LogList.Items.Count == 0) return;

        LogList.ScrollIntoView(LogList.Items[LogList.Items.Count - 1]);
    }

    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        LogService.EntryAppended -= OnEntryAppended;
        if (_viewModel is not null)
        {
            _viewModel.Entries.CollectionChanged -= OnEntriesChanged;
        }
    }
}