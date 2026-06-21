using Common.WPF.ViewModels;
using RomForge.Models;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Windows;

namespace RomForge.ViewModels;

public abstract class MultiToolTabViewModel : ToolTabViewModel
{
    private int _subTabIndex;
    private readonly List<ToolTabViewModel> _tools = [];

    public int SubTabIndex
    {
        get => _subTabIndex;
        set
        {
            _subTabIndex = value;
            OnPropertyChanged();
            SyncLogEntries();
        }
    }

    public ObservableCollection<LogEntry> LogEntries { get; } = [];

    public List<ToolTabViewModel> Tools => _tools;

    protected void InitializeMultiTools()
    {
        foreach (var tool in _tools)
        {
            RegisterChild(tool);

            var logProp = tool.GetType().GetProperty("LogEntries");
            if (logProp?.GetValue(tool) is ObservableCollection<LogEntry> childLogs)
            {
                childLogs.CollectionChanged += (_, e) => LogEntries_CollectionChanged(e, tool);
            }
        }

        SyncLogEntries();
    }

    private void LogEntries_CollectionChanged(NotifyCollectionChangedEventArgs e, ToolTabViewModel tool)
    {
        if (SubTabIndex < 0 || SubTabIndex >= _tools.Count || _tools[SubTabIndex] != tool)
            return;

        if (Application.Current?.Dispatcher != null)
            Application.Current.Dispatcher.Invoke(() => HandleCollectionChanged(e));
        else
            HandleCollectionChanged(e);
    }

    private void HandleCollectionChanged(NotifyCollectionChangedEventArgs e)
    {
        switch (e.Action)
        {
            case NotifyCollectionChangedAction.Add:
                if (e.NewItems != null)
                    foreach (LogEntry item in e.NewItems) 
                        LogEntries.Add(item);
                break;
            case NotifyCollectionChangedAction.Remove:
                if (e.OldItems != null)
                    foreach (LogEntry item in e.OldItems)
                        LogEntries.Remove(item);
                break;
            case NotifyCollectionChangedAction.Reset:
                LogEntries.Clear();
                break;
        }
    }

    private void SyncLogEntries()
    {
        if (Application.Current?.Dispatcher != null)
            Application.Current.Dispatcher.Invoke(() => DoSync());
        else
            DoSync();
    }

    private void DoSync()
    {
        LogEntries.Clear();

        if (SubTabIndex < 0 || SubTabIndex >= _tools.Count)
            return;

        var currentTool = _tools[SubTabIndex];
        var logProp = currentTool.GetType().GetProperty("LogEntries");

        if (logProp?.GetValue(currentTool) is ObservableCollection<LogEntry> childLogs)
        {
            foreach (var item in childLogs)
                LogEntries.Add(item);
        }
    }
}