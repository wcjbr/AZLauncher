using System.Collections.Generic;
using CommunityToolkit.Mvvm.ComponentModel;
using AZLuncher.Models;
using AZLuncher.Services;

namespace AZLuncher.ViewModels;

public sealed partial class InstancesPageViewModel : LocalizedViewModelBase
{
    public InstancesPageViewModel(LocalizationService localizer) : base(localizer)
    {
        RefreshCollections();
    }

    public string RuntimeSummary => IsChinese
        ? "当前实例全部使用同一套 Java 运行时和分配策略。"
        : "All instances currently share the same Java runtime and memory profile.";

    public string ActiveQueueSummary => IsChinese
        ? "启动队列已完成资源预热，只需要选择一个实例即可开始。"
        : "The launch queue is warmed up; choose an instance and start immediately.";

    public string SectionLabel => IsChinese ? "实例管理" : "Instance management";

    public string SectionHeading => IsChinese ? "切换不同配置与启动链路" : "Switch across profiles and launch paths";

    public string RuntimeCardTitle => IsChinese ? "统一运行时" : "Shared runtime";

    public string QueueCardTitle => IsChinese ? "启动队列" : "Launch queue";

    public string RecommendedLabel => IsChinese ? "推荐" : "Recommended";

    [ObservableProperty]
    private IReadOnlyList<LauncherVersion> installedVersions = [];

    protected override void OnLanguageChanged()
    {
        RefreshCollections();
        RaiseProperties(
            nameof(RuntimeSummary),
            nameof(ActiveQueueSummary),
            nameof(SectionLabel),
            nameof(SectionHeading),
            nameof(RuntimeCardTitle),
            nameof(QueueCardTitle),
            nameof(RecommendedLabel));
    }

    private void RefreshCollections()
    {
        InstalledVersions = IsChinese ?
        [
            new LauncherVersion
            {
                Name = "Survival Fabric",
                Channel = "轻量",
                Summary = "日常生存主实例，启用地图、优化和光影。",
                LastPlayed = "默认启动目标",
                IsRecommended = true,
                BadgeText = "推荐",
            },
            new LauncherVersion
            {
                Name = "Creative Testbed",
                Channel = "测试",
                Summary = "用于材质、命令和区块实验的独立配置。",
                LastPlayed = "保留独立资源缓存",
            },
            new LauncherVersion
            {
                Name = "Automation Pack",
                Channel = "重型",
                Summary = "工业向大型整合包，适合单独配置内存和模组版本。",
                LastPlayed = "需要较长首启时间",
            },
        ]
        :
        [
            new LauncherVersion
            {
                Name = "Survival Fabric",
                Channel = "Lightweight",
                Summary = "Daily survival instance with map tools, optimizations, and shaders.",
                LastPlayed = "Default launch target",
                IsRecommended = true,
                BadgeText = "Recommended",
            },
            new LauncherVersion
            {
                Name = "Creative Testbed",
                Channel = "Testing",
                Summary = "Separate profile for textures, commands, and chunk experiments.",
                LastPlayed = "Keeps its own asset cache",
            },
            new LauncherVersion
            {
                Name = "Automation Pack",
                Channel = "Heavy",
                Summary = "Industry-focused modpack that benefits from custom memory and mod tuning.",
                LastPlayed = "Longer first boot expected",
            },
        ];
    }
}
