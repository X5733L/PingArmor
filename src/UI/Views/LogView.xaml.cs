using System;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
using PingArmor.Services;
using PingArmor.UI.ViewModels;

namespace PingArmor.UI.Views;

public partial class LogView : UserControl
{
    private readonly LogViewModel? _viewModel;
    private ScrollViewer? _scrollViewer;

    public LogView()
    {
        InitializeComponent();

        if (AppServices.IsReady)
        {
            _viewModel = new LogViewModel(AppServices.Monitor!, AppServices.LogAppender);
            DataContext = _viewModel;
            _viewModel.Entries.CollectionChanged += OnEntriesChanged;
            _viewModel.PropertyChanged += OnViewModelPropertyChanged;
        }

        LogService.EntryAppended += OnEntryAppended;
        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
    }

    /// <summary>
    /// Pins the list to the newest entry. Used when the page becomes visible,
    /// when auto-scroll is enabled and by the "scroll to bottom" button.
    /// </summary>
    public void ScrollToEnd()
    {
        // Defer until the list has performed layout, otherwise ScrollIntoView is a no-op.
        Dispatcher.BeginInvoke(new Action(() =>
        {
            _scrollViewer ??= FindDescendant<ScrollViewer>(LogList);
            if (LogList.Items.Count == 0)
            {
                return;
            }

            LogList.ScrollIntoView(LogList.Items[LogList.Items.Count - 1]);
            _scrollViewer?.ScrollToEnd();
        }), DispatcherPriority.Background);
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        _scrollViewer ??= FindDescendant<ScrollViewer>(LogList);
        ScrollToEnd();
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
        if (_viewModel?.AutoScroll != true) return;

        if (e.Action is NotifyCollectionChangedAction.Add
            or NotifyCollectionChangedAction.Reset
            or NotifyCollectionChangedAction.Replace)
        {
            ScrollToEnd();
        }
    }

    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        // Enabling the checkbox should immediately jump to the newest entry.
        if (e.PropertyName == nameof(LogViewModel.AutoScroll) && _viewModel?.AutoScroll == true)
        {
            ScrollToEnd();
        }
    }

    private void BtnScrollToBottom_Click(object sender, RoutedEventArgs e) => ScrollToEnd();

    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        LogService.EntryAppended -= OnEntryAppended;

        if (_viewModel is not null)
        {
            _viewModel.Entries.CollectionChanged -= OnEntriesChanged;
            _viewModel.PropertyChanged -= OnViewModelPropertyChanged;
        }
    }

    private static T? FindDescendant<T>(DependencyObject root) where T : DependencyObject
    {
        int count = VisualTreeHelper.GetChildrenCount(root);
        for (int i = 0; i < count; i++)
        {
            DependencyObject child = VisualTreeHelper.GetChild(root, i);
            if (child is T match)
            {
                return match;
            }

            T? nested = FindDescendant<T>(child);
            if (nested is not null)
            {
                return nested;
            }
        }

        return null;
    }
}