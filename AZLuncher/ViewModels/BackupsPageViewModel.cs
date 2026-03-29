using System.Collections.Generic;
using CommunityToolkit.Mvvm.ComponentModel;
using AZLuncher.Models;
using AZLuncher.Services;

namespace AZLuncher.ViewModels;

public sealed partial class BackupsPageViewModel : LocalizedViewModelBase
{
    public BackupsPageViewModel(LocalizationService localizer) : base(localizer)
    {
        RefreshCollections();
    }

    public string SectionLabel => IsChinese ? "世界备份" : "World backups";

    public string SectionHeading => IsChinese ? "保留快照并追踪恢复点" : "Keep snapshots and track restore points";

    public string PolicyTitle => IsChinese ? "备份策略" : "Backup policy";

    public string PolicyBody => IsChinese
        ? "当前为每日增量、每周完整备份，并在清理前保留最近七个恢复点。"
        : "Daily incremental and weekly full backups keep the latest seven restore points before rotation.";

    public string RestoreTitle => IsChinese ? "恢复准备" : "Restore readiness";

    public string RestoreBody => IsChinese
        ? "最后一次完整校验通过，恢复流程可以直接绑定到后续真实文件逻辑。"
        : "The last validation passed; the restore flow is ready to be wired into real filesystem operations.";

    [ObservableProperty]
    private IReadOnlyList<BackupSnapshot> snapshots = [];

    protected override void OnLanguageChanged()
    {
        RefreshCollections();
        RaiseProperties(
            nameof(SectionLabel),
            nameof(SectionHeading),
            nameof(PolicyTitle),
            nameof(PolicyBody),
            nameof(RestoreTitle),
            nameof(RestoreBody));
    }

    private void RefreshCollections()
    {
        Snapshots = IsChinese ?
        [
            new BackupSnapshot
            {
                Name = "Survival_Main",
                CreatedAt = "今天 03:00",
                Size = "1.8 GB",
                Status = "已验证",
                Summary = "主生存世界快照，包含最近一次下界探险后的区块变化。",
            },
            new BackupSnapshot
            {
                Name = "Creative_Labs",
                CreatedAt = "昨天 03:00",
                Size = "640 MB",
                Status = "已归档",
                Summary = "命令与红石测试世界，适合回滚实验前状态。",
            },
            new BackupSnapshot
            {
                Name = "Automation_Pack",
                CreatedAt = "3 天前 03:00",
                Size = "2.6 GB",
                Status = "待清理",
                Summary = "重型整合包世界，已进入轮换窗口但尚未删除。",
            },
        ]
        :
        [
            new BackupSnapshot
            {
                Name = "Survival_Main",
                CreatedAt = "Today 03:00",
                Size = "1.8 GB",
                Status = "Verified",
                Summary = "Primary survival snapshot with the latest Nether exploration changes included.",
            },
            new BackupSnapshot
            {
                Name = "Creative_Labs",
                CreatedAt = "Yesterday 03:00",
                Size = "640 MB",
                Status = "Archived",
                Summary = "Command and redstone testing world that is useful as a rollback point.",
            },
            new BackupSnapshot
            {
                Name = "Automation_Pack",
                CreatedAt = "3 days ago 03:00",
                Size = "2.6 GB",
                Status = "Pending cleanup",
                Summary = "Heavy modded world that has entered the retention window but is not deleted yet.",
            },
        ];
    }
}
