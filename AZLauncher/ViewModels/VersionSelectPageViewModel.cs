using System.Collections.Generic;
using System.ComponentModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using AZLauncher.Models;
using AZLauncher.Services;

namespace AZLauncher.ViewModels;

public sealed partial class VersionSelectPageViewModel : LocalizedViewModelBase
{
    private readonly LauncherStateService launcherState;

    public VersionSelectPageViewModel(LocalizationService localizer, LauncherStateService launcherState) : base(localizer)
    {
        this.launcherState = launcherState;
        this.launcherState.PropertyChanged += HandleLauncherStateChanged;
        RefreshCollections();
    }

    public string SectionLabel => IsChinese ? "版本选择" : "Version Select";

    public string SectionHeading => IsChinese ? "单独切换当前游戏版本与实例" : "Switch the current game version and instance";

    public string SectionSummary => IsChinese
        ? "这里单独显示所有实例与探测到的版本，便于在多个游戏目录之间快速切换。"
        : "This page lists all built-in and detected versions so you can switch quickly across multiple game folders.";

    public string CurrentVersionLabel => IsChinese ? "当前版本" : "Current selection";

    public string SourcePathLabel => IsChinese ? "来源目录" : "Source folder";

    public string ActivateLabel => IsChinese ? "切换到此版本" : "Switch to this version";

    public string RefreshDetectLabel => IsChinese ? "重新探测" : "Refresh detection";

    [ObservableProperty]
    private IReadOnlyList<InstanceSelectionItem> availableVersions = [];

    protected override void OnLanguageChanged()
    {
        RefreshCollections();
        RaiseProperties(
            nameof(SectionLabel),
            nameof(SectionHeading),
            nameof(SectionSummary),
            nameof(CurrentVersionLabel),
            nameof(SourcePathLabel),
            nameof(ActivateLabel),
            nameof(RefreshDetectLabel));
    }

    [RelayCommand]
    private void ActivateVersion(string? versionId)
    {
        if (!string.IsNullOrWhiteSpace(versionId))
        {
            launcherState.ActivateVersion(versionId);
        }
    }

    [RelayCommand]
    private void RefreshDetectedVersions()
    {
        launcherState.DetectInstalledVersions();
        RefreshCollections();
    }

    private void RefreshCollections()
    {
        AvailableVersions = launcherState.GetLocalizedInstanceSelections(IsChinese);
    }

    private void HandleLauncherStateChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(LauncherStateService.CurrentVersionId)
            or nameof(LauncherStateService.Instances)
            or nameof(LauncherStateService.InstallPath))
        {
            RefreshCollections();
        }
    }
}
