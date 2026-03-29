using System.Collections.Generic;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using AZLuncher.Models;
using AZLuncher.Services;

namespace AZLuncher.ViewModels;

public enum LibraryTab
{
    Mods,
    ResourcePacks,
    Shaders,
}

public sealed partial class LibraryPageViewModel : LocalizedViewModelBase
{
    private readonly LauncherStateService launcherState;

    public LibraryPageViewModel(LocalizationService localizer, LauncherStateService launcherState) : base(localizer)
    {
        this.launcherState = launcherState;
        RefreshCollections();
    }

    public string SectionLabel => IsChinese ? "模组库" : "Mod library";

    public string SectionHeading => IsChinese ? "整理模组、资源包与光影" : "Organize mods, packs, and shaders";

    public string SummaryTitle => IsChinese ? "当前库状态" : "Library state";

    public string SummaryBody => IsChinese
        ? "资源库已按模组、资源包和光影拆开，方便在安装前先独立整理。"
        : "The library is split into mods, resource packs, and shaders before installation.";

    public string ToolchainTitle => IsChinese ? "工具链" : "Toolchain";

    public string ToolchainBody => IsChinese
        ? "已准备 Fabric、NeoForge、资源包与光影导入流程，后续可接入真实下载逻辑。"
        : "Fabric, NeoForge, resource-pack, and shader import flows are staged for real download hooks.";

    public string TabsLabel => IsChinese ? "内容分类" : "Content categories";

    public string ModsTabLabel => IsChinese ? "模组" : "Mods";

    public string ResourcePacksTabLabel => IsChinese ? "资源包" : "Resource Packs";

    public string ShadersTabLabel => IsChinese ? "光影" : "Shaders";

    public string ActiveTabLabel => CurrentTab switch
    {
        LibraryTab.Mods => ModsTabLabel,
        LibraryTab.ResourcePacks => ResourcePacksTabLabel,
        LibraryTab.Shaders => ShadersTabLabel,
        _ => string.Empty,
    };

    public string ActiveTabSummary => CurrentTab switch
    {
        LibraryTab.Mods => IsChinese
            ? "这里集中放功能模组、性能优化和整合包组件。"
            : "This tab holds gameplay mods, performance tools, and pack components.",
        LibraryTab.ResourcePacks => IsChinese
            ? "这里用于管理材质包、UI 贴图和风格化资源覆盖。"
            : "This tab manages textures, UI skins, and visual resource overrides.",
        LibraryTab.Shaders => IsChinese
            ? "这里用于管理日常生存、截图展示和低负载切换用的光影包。"
            : "This tab manages everyday, cinematic, and low-load shader packs.",
        _ => string.Empty,
    };

    public bool IsModsTabSelected => CurrentTab == LibraryTab.Mods;

    public bool IsResourcePacksTabSelected => CurrentTab == LibraryTab.ResourcePacks;

    public bool IsShadersTabSelected => CurrentTab == LibraryTab.Shaders;

    [ObservableProperty]
    private IReadOnlyList<LibraryItem> libraryItems = [];

    [ObservableProperty]
    private LibraryTab currentTab = LibraryTab.Mods;

    public IReadOnlyList<LibraryItem> VisibleLibraryItems => LibraryItems;

    partial void OnCurrentTabChanged(LibraryTab value)
    {
        RefreshCollections();
        RaiseTabProperties();
    }

    protected override void OnLanguageChanged()
    {
        RefreshCollections();
        RaiseProperties(
            nameof(SectionLabel),
            nameof(SectionHeading),
            nameof(SummaryTitle),
            nameof(SummaryBody),
            nameof(ToolchainTitle),
            nameof(ToolchainBody),
            nameof(TabsLabel),
            nameof(ModsTabLabel),
            nameof(ResourcePacksTabLabel),
            nameof(ShadersTabLabel),
            nameof(ActiveTabLabel),
            nameof(ActiveTabSummary));
        RaiseTabProperties();
    }

    private void RefreshCollections()
    {
        LibraryItems = CurrentTab switch
        {
            LibraryTab.Mods => launcherState.GetLocalizedMods(IsChinese),
            LibraryTab.ResourcePacks => launcherState.GetLocalizedResourcePacks(IsChinese),
            LibraryTab.Shaders => launcherState.GetLocalizedShaders(IsChinese),
            _ => [],
        };

        RaiseProperties(nameof(VisibleLibraryItems));
    }

    [RelayCommand]
    private void ShowMods()
    {
        CurrentTab = LibraryTab.Mods;
    }

    [RelayCommand]
    private void ShowResourcePacks()
    {
        CurrentTab = LibraryTab.ResourcePacks;
    }

    [RelayCommand]
    private void ShowShaders()
    {
        CurrentTab = LibraryTab.Shaders;
    }

    private void RaiseTabProperties()
    {
        RaiseProperties(
            nameof(ActiveTabLabel),
            nameof(ActiveTabSummary),
            nameof(IsModsTabSelected),
            nameof(IsResourcePacksTabSelected),
            nameof(IsShadersTabSelected),
            nameof(VisibleLibraryItems));
    }

}
