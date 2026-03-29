using System.Collections.Generic;
using CommunityToolkit.Mvvm.ComponentModel;
using AZLuncher.Models;
using AZLuncher.Services;

namespace AZLuncher.ViewModels;

public sealed partial class LibraryPageViewModel : LocalizedViewModelBase
{
    public LibraryPageViewModel(LocalizationService localizer) : base(localizer)
    {
        RefreshCollections();
    }

    public string SectionLabel => IsChinese ? "模组库" : "Mod library";

    public string SectionHeading => IsChinese ? "整理模组、资源包与预设" : "Organize mods, packs, and presets";

    public string SummaryTitle => IsChinese ? "当前库状态" : "Library state";

    public string SummaryBody => IsChinese
        ? "资源库已分成稳定、试验和待审核三个分区，方便在安装前先整理。"
        : "The library is split into stable, experimental, and review lanes before installation.";

    public string ToolchainTitle => IsChinese ? "工具链" : "Toolchain";

    public string ToolchainBody => IsChinese
        ? "已准备 Fabric、NeoForge 与资源包导入流程，后续可接入真实下载逻辑。"
        : "Fabric, NeoForge, and resource-pack import flows are staged and ready for real download hooks.";

    [ObservableProperty]
    private IReadOnlyList<LibraryItem> libraryItems = [];

    protected override void OnLanguageChanged()
    {
        RefreshCollections();
        RaiseProperties(
            nameof(SectionLabel),
            nameof(SectionHeading),
            nameof(SummaryTitle),
            nameof(SummaryBody),
            nameof(ToolchainTitle),
            nameof(ToolchainBody));
    }

    private void RefreshCollections()
    {
        LibraryItems = IsChinese ?
        [
            new LibraryItem
            {
                Name = "Sodium + Iris",
                Category = "性能 / 光影",
                State = "已安装",
                Summary = "主力图形优化组合，适用于 Fabric 生存配置。",
            },
            new LibraryItem
            {
                Name = "Create Suite",
                Category = "工业整合",
                State = "待审核",
                Summary = "大型工业模组集合，建议单独放进重型实例里。",
            },
            new LibraryItem
            {
                Name = "Faithful 32x",
                Category = "资源包",
                State = "可启用",
                Summary = "保留原版风格的高清材质包，可按实例开关。",
            },
        ]
        :
        [
            new LibraryItem
            {
                Name = "Sodium + Iris",
                Category = "Performance / Shaders",
                State = "Installed",
                Summary = "Primary graphics stack for the Fabric survival profile.",
            },
            new LibraryItem
            {
                Name = "Create Suite",
                Category = "Automation pack",
                State = "Review",
                Summary = "Large-scale automation collection that fits best in a heavy instance.",
            },
            new LibraryItem
            {
                Name = "Faithful 32x",
                Category = "Resource pack",
                State = "Available",
                Summary = "High-resolution pack that preserves the vanilla look and can be toggled per instance.",
            },
        ];
    }
}
