using RomForge.Core.ViewModels.Switch;
using System.IO;

namespace RomForge.Core.Models.Switch;

public class RenameFileItem(string filePath) : GameFile(filePath)
{

    public string OldName => Path.GetFileName(FilePath);

    public string NewName { get; private set => SetProperty(ref field, value); } = string.Empty;

    public string StatusColor { get; private set => SetProperty(ref field, value); } = "#888888";

    public void ApplyPlan(RenamePlan plan, string? statusOverride = null)
    {
        if (statusOverride is not null)
        {
            NewName = "-";
            Status = statusOverride;
            StatusColor = "#888888";
            return;
        }

        NewName = plan.State is RenameState.MissingInfo or RenameState.EmptyName ? "-" : plan.NewName;

        Status = plan.State switch
        {
            RenameState.Ready => "변경 예정",
            RenameState.Unchanged => "변경 없음",
            RenameState.MissingInfo => "정보 없음",
            RenameState.EmptyName => "이름 없음",
            RenameState.NameTooLong => "이름 너무 김",
            RenameState.Duplicate => "이름 중복",
            RenameState.TargetExists => "파일 존재",
            _ => string.Empty
        };

        StatusColor = plan.State switch
        {
            RenameState.Ready => "#4CAF50",
            RenameState.Unchanged => "#888888",
            RenameState.MissingInfo => "#FFD250",
            _ => "#FF6464"
        };
    }
}