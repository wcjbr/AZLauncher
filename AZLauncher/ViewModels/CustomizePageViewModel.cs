using System;
using System.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using AZLauncher.Models;
using AZLauncher.Services;

namespace AZLauncher.ViewModels;

public sealed partial class CustomizePageViewModel : LocalizedViewModelBase
{
    private readonly AppConfigService configService;
    private readonly ThemeCustomizationService themeService;

    public CustomizePageViewModel(
        LocalizationService localizer,
        ThemeCustomizationService themeService,
        AppConfigService configService) : base(localizer)
    {
        this.configService = configService;
        this.themeService = themeService;
        this.themeService.AppearanceChanged += HandleAppearanceChanged;
        this.configService.PropertyChanged += HandleConfigPropertyChanged;
    }

    public string SectionLabel => IsChinese ? "界面自定义" : "Interface customization";

    public string SectionHeading => IsChinese ? "把启动器调成你自己的样子" : "Tune the launcher to your own taste";

    public string SectionSummary => IsChinese
        ? "主题、密度和圆角会立即应用到整个界面。"
        : "Theme, density, and corner style apply to the whole launcher immediately.";

    public string ThemeGroupLabel => IsChinese ? "主题预设" : "Theme preset";

    public string DensityGroupLabel => IsChinese ? "界面密度" : "Interface density";

    public string ShapeGroupLabel => IsChinese ? "圆角风格" : "Corner style";

    public string BrandingGroupLabel => IsChinese ? "启动器标题" : "Launcher title";

    public string BrandingTitle => IsChinese ? "左侧主标题" : "Sidebar title";

    public string BrandingSummary => IsChinese
        ? "这个标题会显示在侧边栏和窗口标题上，并保存到本地配置文件。"
        : "This title is used in the sidebar and window title, then persisted to the local config file.";

    public string LauncherTitle
    {
        get => configService.LauncherTitle;
        set => configService.LauncherTitle = value;
    }

    public string ConfigPathLabel => IsChinese ? "配置文件" : "Config file";

    public string ConfigPath => configService.ConfigFilePath;

    public string PreviewTitle => IsChinese ? "实时效果" : "Live preview";

    public string PreviewSummary => IsChinese
        ? "切换任何选项后，侧边栏、按钮、卡片和主视觉色板会同步更新。"
        : "Sidebar, buttons, cards, and the hero palette update together after every change.";

    public string ResetLabel => IsChinese ? "恢复默认外观" : "Restore defaults";

    public string ActiveLabel => IsChinese ? "当前启用" : "Active";

    public string PreviewEyebrow => IsChinese ? "界面示例" : "Interface sample";

    public string PreviewHeroTitle => IsChinese ? "新的视觉会直接套用" : "The new look applies immediately";

    public string PreviewHeroSummary => IsChinese
        ? "你不需要重启启动器，当前窗口会直接反映新的配色和节奏。"
        : "You do not need to restart; the current window reflects the new palette and rhythm instantly.";

    public string PreviewPrimaryAction => IsChinese ? "启动游戏" : "Launch game";

    public string PreviewSecondaryAction => IsChinese ? "浏览实例" : "Browse instances";

    public string CurrentThemeLabel => IsChinese ? "当前主题" : "Current theme";

    public string CurrentDensityLabel => IsChinese ? "当前密度" : "Current density";

    public string CurrentShapeLabel => IsChinese ? "当前风格" : "Current style";

    public string CurrentTitleLabel => IsChinese ? "当前标题" : "Current title";

    public string MossTitle => IsChinese ? "苔原绿" : "Moss";

    public string MossSummary => IsChinese ? "保留目前的自然矿洞风格。" : "The current earthy launcher look.";

    public string MidnightTitle => IsChinese ? "夜幕蓝" : "Midnight";

    public string MidnightSummary => IsChinese ? "更偏冷色、适合低光环境。" : "A cooler palette for low-light setups.";

    public string EmberTitle => IsChinese ? "余烬橙" : "Ember";

    public string EmberSummary => IsChinese ? "更暖、更强烈的火光风格。" : "A warmer, more dramatic ember palette.";

    public string ComfortableTitle => IsChinese ? "舒展" : "Comfortable";

    public string ComfortableSummary => IsChinese ? "更宽松的卡片和按钮留白。" : "Looser spacing for cards and controls.";

    public string CompactTitle => IsChinese ? "紧凑" : "Compact";

    public string CompactSummary => IsChinese ? "更高的信息密度和更短的按钮高度。" : "Higher information density with tighter controls.";

    public string RoundedTitle => IsChinese ? "圆润" : "Rounded";

    public string RoundedSummary => IsChinese ? "保留更柔和的卡片和按钮轮廓。" : "Keep the softer card and button silhouette.";

    public string DefinedTitle => IsChinese ? "利落" : "Defined";

    public string DefinedSummary => IsChinese ? "收紧圆角，界面会更硬朗一些。" : "Tighten corners for a firmer, sharper shell.";

    public bool IsMossSelected => themeService.SelectedPreset == UiThemePreset.Moss;

    public bool IsMidnightSelected => themeService.SelectedPreset == UiThemePreset.Midnight;

    public bool IsEmberSelected => themeService.SelectedPreset == UiThemePreset.Ember;

    public bool IsComfortableSelected => themeService.SelectedDensity == UiDensity.Comfortable;

    public bool IsCompactSelected => themeService.SelectedDensity == UiDensity.Compact;

    public bool IsRoundedSelected => themeService.SelectedShape == UiShape.Rounded;

    public bool IsDefinedSelected => themeService.SelectedShape == UiShape.Defined;

    public string CurrentThemeName => themeService.SelectedPreset switch
    {
        UiThemePreset.Moss => MossTitle,
        UiThemePreset.Midnight => MidnightTitle,
        UiThemePreset.Ember => EmberTitle,
        _ => string.Empty,
    };

    public string CurrentDensityName => themeService.SelectedDensity switch
    {
        UiDensity.Comfortable => ComfortableTitle,
        UiDensity.Compact => CompactTitle,
        _ => string.Empty,
    };

    public string CurrentShapeName => themeService.SelectedShape switch
    {
        UiShape.Rounded => RoundedTitle,
        UiShape.Defined => DefinedTitle,
        _ => string.Empty,
    };

    protected override void OnLanguageChanged()
    {
        RaiseProperties(
            nameof(SectionLabel),
            nameof(SectionHeading),
            nameof(SectionSummary),
            nameof(ThemeGroupLabel),
            nameof(DensityGroupLabel),
            nameof(ShapeGroupLabel),
            nameof(BrandingGroupLabel),
            nameof(BrandingTitle),
            nameof(BrandingSummary),
            nameof(ConfigPathLabel),
            nameof(ConfigPath),
            nameof(PreviewTitle),
            nameof(PreviewSummary),
            nameof(ResetLabel),
            nameof(ActiveLabel),
            nameof(PreviewEyebrow),
            nameof(PreviewHeroTitle),
            nameof(PreviewHeroSummary),
            nameof(PreviewPrimaryAction),
            nameof(PreviewSecondaryAction),
            nameof(CurrentThemeLabel),
            nameof(CurrentDensityLabel),
            nameof(CurrentShapeLabel),
            nameof(CurrentTitleLabel),
            nameof(MossTitle),
            nameof(MossSummary),
            nameof(MidnightTitle),
            nameof(MidnightSummary),
            nameof(EmberTitle),
            nameof(EmberSummary),
            nameof(ComfortableTitle),
            nameof(ComfortableSummary),
            nameof(CompactTitle),
            nameof(CompactSummary),
            nameof(RoundedTitle),
            nameof(RoundedSummary),
            nameof(DefinedTitle),
            nameof(DefinedSummary),
            nameof(LauncherTitle),
            nameof(CurrentThemeName),
            nameof(CurrentDensityName),
            nameof(CurrentShapeName));
    }

    [RelayCommand]
    private void UseMossTheme()
    {
        themeService.SelectedPreset = UiThemePreset.Moss;
    }

    [RelayCommand]
    private void UseMidnightTheme()
    {
        themeService.SelectedPreset = UiThemePreset.Midnight;
    }

    [RelayCommand]
    private void UseEmberTheme()
    {
        themeService.SelectedPreset = UiThemePreset.Ember;
    }

    [RelayCommand]
    private void UseComfortableDensity()
    {
        themeService.SelectedDensity = UiDensity.Comfortable;
    }

    [RelayCommand]
    private void UseCompactDensity()
    {
        themeService.SelectedDensity = UiDensity.Compact;
    }

    [RelayCommand]
    private void UseRoundedShape()
    {
        themeService.SelectedShape = UiShape.Rounded;
    }

    [RelayCommand]
    private void UseDefinedShape()
    {
        themeService.SelectedShape = UiShape.Defined;
    }

    [RelayCommand]
    private void ResetAppearance()
    {
        themeService.ResetDefaults();
    }

    private void HandleAppearanceChanged(object? sender, EventArgs e)
    {
        RaiseProperties(
            nameof(IsMossSelected),
            nameof(IsMidnightSelected),
            nameof(IsEmberSelected),
            nameof(IsComfortableSelected),
            nameof(IsCompactSelected),
            nameof(IsRoundedSelected),
            nameof(IsDefinedSelected),
            nameof(CurrentThemeName),
            nameof(CurrentDensityName),
            nameof(CurrentShapeName));
    }

    private void HandleConfigPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(AppConfigService.LauncherTitle))
        {
            RaiseProperties(nameof(LauncherTitle));
        }
    }
}
