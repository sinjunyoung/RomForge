using Common.WPF.ViewModels;
using RomForge.Models;
using System.Collections.ObjectModel;

namespace RomForge.ViewModels.Switch;

public class MergeMainViewModel: ToolTabViewModel
{
    public ObservableCollection<LogEntry> LogEntries { get; } = [];

    public MergeMainViewModel() { }
}