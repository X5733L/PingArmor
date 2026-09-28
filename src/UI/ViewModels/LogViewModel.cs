using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Windows.Data;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PingArmor.Services;

namespace PingArmor.UI.ViewModels;

/// <summary>View model for the event log page.</summary>
public sealed partial class LogViewModel : ViewModelBase
{
    private readonly NetworkMonitor _monitor;
    private readonly Action<string> _logAppender;

    public ObservableCollection<LogEntry> Entries { get; } = new();
    public ICollectionView EntriesView { get; }

    [ObservableProperty]
    private string _searchText = string.Empty;

    [ObservableProperty]
    private bool _autoScroll = true;

    [ObservableProperty]
    private LogLevel? _levelFilter;

    public bool IsLevelAllSelected => LevelFilter is null;
    public bool IsLevelDebugSelected => LevelFilter == LogLevel.Debug;
    public bool IsLevelInfoSelected => LevelFilter == LogLevel.Info;
    public bool IsLevelWarnSelected => LevelFilter == LogLevel.Warn;
    public bool IsLevelErrorSelected => LevelFilter == LogLevel.Error;

    public int TotalCount => Entries.Count;
    public int DebugCount => Entries.Count(e => e.Level == LogLevel.Debug);
    public int InfoCount => Entries.Count(e => e.Level == LogLevel.Info);
    public int WarnCount => Entries.Count(e => e.Level == LogLevel.Warn);
    public int ErrorCount => Entries.Count(e => e.Level == LogLevel.Error);

    public string AllChipText => $"{Texts.LogFilterAll} ({TotalCount})";
    public string DebugChipText => $"Debug ({DebugCount})";
    public string InfoChipText => $"Info ({InfoCount})";
    public string WarnChipText => $"Warn ({WarnCount})";
    public string ErrorChipText => $"Error ({ErrorCount})";

    public bool IsEmpty => Entries.Count == 0;

    public LogViewModel(NetworkMonitor monitor, Action<string> logAppender)
    {
        _monitor = monitor;
        _logAppender = logAppender;

        EntriesView = CollectionViewSource.GetDefaultView(Entries);
        EntriesView.Filter = MatchesFilter;
        LoadRecent();
    }

    public void LoadRecent()
    {
        Entries.Clear();
        foreach (var entry in LogService.GetRecentEntries())
        {
            Entries.Add(entry);
        }
        RaiseCounts();
    }

    public void AppendEntry(LogEntry entry)
    {
        Entries.Add(entry);
        while (Entries.Count > LogService.MaxMemoryLines)
        {
            Entries.RemoveAt(0);
        }
        RaiseCounts();
    }

    [RelayCommand]
    private void SetLevelFilter(string? filter)
    {
        LevelFilter = filter switch
        {
            "Debug" => LogLevel.Debug,
            "Info" => LogLevel.Info,
            "Warn" => LogLevel.Warn,
            "Error" => LogLevel.Error,
            _ => null
        };
    }

    [RelayCommand]
    private void Clear()
    {
        Entries.Clear();
        LogService.ClearMemory();
        RaiseCounts();
    }

    [RelayCommand]
    private void Copy()
    {
        try
        {
            string text = string.Join(Environment.NewLine, EntriesView.Cast<LogEntry>().Select(e => e.ToDisplayString()));
            if (text.Length == 0) return;

            System.Windows.Clipboard.SetText(text);
            _logAppender(Texts.LogCopiedToast);
        }
        catch (Exception ex)
        {
            _logAppender($"[-] Failed to copy log: {ex.Message}");
        }
    }

    [RelayCommand]
    private void OpenFile()
    {
        try
        {
            LogService.Flush();
            string path = LogService.LogFilePath;
            if (!File.Exists(path))
            {
                File.WriteAllText(path, string.Empty);
            }

            Process.Start(new ProcessStartInfo
            {
                FileName = path,
                UseShellExecute = true
            });
        }
        catch (Exception ex)
        {
            _logAppender($"[-] Failed to open log file: {ex.Message}");
        }
    }

    [RelayCommand]
    private void Optimize() => _monitor.TriggerManualCheck();

    partial void OnSearchTextChanged(string value) => EntriesView?.Refresh();

    partial void OnLevelFilterChanged(LogLevel? value)
    {
        EntriesView?.Refresh();
        OnPropertyChanged(nameof(IsLevelAllSelected));
        OnPropertyChanged(nameof(IsLevelDebugSelected));
        OnPropertyChanged(nameof(IsLevelInfoSelected));
        OnPropertyChanged(nameof(IsLevelWarnSelected));
        OnPropertyChanged(nameof(IsLevelErrorSelected));
    }

    protected override void OnLanguageChangedCore() => RaiseCounts();

    private bool MatchesFilter(object item)
    {
        if (item is not LogEntry entry) return false;

        if (LevelFilter is { } level && entry.Level != level) return false;

        if (!string.IsNullOrWhiteSpace(SearchText))
        {
            string term = SearchText.Trim();
            if (!entry.Message.Contains(term, StringComparison.OrdinalIgnoreCase)
                && !entry.LevelTag.Contains(term, StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }
        }

        return true;
    }

    private void RaiseCounts()
    {
        OnPropertyChanged(nameof(TotalCount));
        OnPropertyChanged(nameof(DebugCount));
        OnPropertyChanged(nameof(InfoCount));
        OnPropertyChanged(nameof(WarnCount));
        OnPropertyChanged(nameof(ErrorCount));
        OnPropertyChanged(nameof(AllChipText));
        OnPropertyChanged(nameof(DebugChipText));
        OnPropertyChanged(nameof(InfoChipText));
        OnPropertyChanged(nameof(WarnChipText));
        OnPropertyChanged(nameof(ErrorChipText));
        OnPropertyChanged(nameof(IsEmpty));
    }
}