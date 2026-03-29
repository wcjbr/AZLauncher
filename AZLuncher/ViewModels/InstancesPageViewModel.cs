using System.Collections.Generic;
using System.ComponentModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using AZLuncher.Models;
using AZLuncher.Services;

namespace AZLuncher.ViewModels;

public sealed partial class InstancesPageViewModel : LocalizedViewModelBase
{
    private readonly LauncherStateService launcherState;

    public InstancesPageViewModel(LocalizationService localizer, LauncherStateService launcherState) : base(localizer)
    {
        this.launcherState = launcherState;
        this.launcherState.PropertyChanged += HandleLauncherStateChanged;
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

    public string SetActiveLabel => IsChinese ? "设为当前" : "Set active";

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
            nameof(RecommendedLabel),
            nameof(SetActiveLabel));
    }

    private void RefreshCollections()
    {
        InstalledVersions = launcherState.GetLocalizedVersions(IsChinese);
    }

    [RelayCommand]
    private void ActivateVersion(string? versionId)
    {
        if (!string.IsNullOrWhiteSpace(versionId))
        {
            launcherState.ActivateVersion(versionId);
        }
    }

    private void HandleLauncherStateChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(LauncherStateService.CurrentVersionId))
        {
            RefreshCollections();
        }
    }
}
