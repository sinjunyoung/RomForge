using Common.Localization;
using RomForge.Core.ViewModels.Switch;
using System.IO;

namespace RomForge.Core.Models.Switch;

public class RenameFileItem(string filePath) : GameFile(filePath)
{
    private RenameState? _planState;

    public string OldName => Path.GetFileName(FilePath);

    public string NewName { get; private set => SetProperty(ref field, value); } = string.Empty;

    public string StatusColor { get; private set => SetProperty(ref field, value); } = "#888888";

    public override string Status => _planState is { } state ? DescribePlan(state) : base.Status;

    public void ApplyPlan(RenamePlan plan, string? statusOverride = null)
    {
        if (statusOverride is not null)
        {
            NewName = "-";
            _planState = null;
            SetStatusText(statusOverride);
            StatusColor = "#888888";
            return;
        }

        NewName = plan.State is RenameState.MissingInfo or RenameState.EmptyName ? "-" : plan.NewName;

        _planState = plan.State;
        OnPropertyChanged(nameof(Status));

        StatusColor = plan.State switch
        {
            RenameState.Ready => "#4CAF50",
            RenameState.Unchanged => "#888888",
            RenameState.MissingInfo => "#FFD250",
            _ => "#FF6464"
        };
    }

    private static string DescribePlan(RenameState state) => state switch
    {
        RenameState.Ready => Loc.S.RenameStatusReady,
        RenameState.Unchanged => Loc.S.RenameStatusUnchanged,
        RenameState.MissingInfo => Loc.S.RenameStatusMissingInfo,
        RenameState.EmptyName => Loc.S.RenameStatusEmptyName,
        RenameState.NameTooLong => Loc.S.RenameStatusNameTooLong,
        RenameState.Duplicate => Loc.S.RenameStatusDuplicate,
        RenameState.TargetExists => Loc.S.RenameStatusTargetExists,
        _ => string.Empty
    };
}