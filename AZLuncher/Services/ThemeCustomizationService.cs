using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using AZLuncher.Models;

namespace AZLuncher.Services;

public sealed partial class ThemeCustomizationService : ObservableObject
{
    private readonly AppConfigService configService;

    public ThemeCustomizationService(AppConfigService configService)
    {
        this.configService = configService;
        selectedPreset = configService.ThemePreset;
        selectedDensity = configService.Density;
        selectedShape = configService.Shape;
        ApplyTheme();
    }

    [ObservableProperty]
    private UiThemePreset selectedPreset = UiThemePreset.Moss;

    [ObservableProperty]
    private UiDensity selectedDensity = UiDensity.Comfortable;

    [ObservableProperty]
    private UiShape selectedShape = UiShape.Rounded;

    public event EventHandler? AppearanceChanged;

    public void ResetDefaults()
    {
        SelectedPreset = UiThemePreset.Moss;
        SelectedDensity = UiDensity.Comfortable;
        SelectedShape = UiShape.Rounded;
        ApplyTheme();
    }

    partial void OnSelectedPresetChanged(UiThemePreset value)
    {
        configService.ThemePreset = value;
        ApplyTheme();
    }

    partial void OnSelectedDensityChanged(UiDensity value)
    {
        configService.Density = value;
        ApplyTheme();
    }

    partial void OnSelectedShapeChanged(UiShape value)
    {
        configService.Shape = value;
        ApplyTheme();
    }

    private void ApplyTheme()
    {
        if (Application.Current?.Resources is not { } resources)
        {
            return;
        }

        var palette = SelectedPreset switch
        {
            UiThemePreset.Moss => new ThemePalette(
                PageBase: Color.Parse("#F0E6D5"),
                Surface: Color.Parse("#FFF8EE"),
                SurfaceAlt: Color.Parse("#E4D7C0"),
                Sidebar: Color.Parse("#22362B"),
                SidebarAlt: Color.Parse("#314B3A"),
                HeroStart: Color.Parse("#6F8C56"),
                HeroEnd: Color.Parse("#2E5B45"),
                Accent: Color.Parse("#50783C"),
                AccentDeep: Color.Parse("#294230"),
                Highlight: Color.Parse("#D2AE68"),
                HighlightAlt: Color.Parse("#B9883C"),
                Ink: Color.Parse("#201D18"),
                MutedInk: Color.Parse("#6B645B"),
                SidebarText: Color.Parse("#F5F0E6"),
                SidebarMutedText: Color.Parse("#C4D0C5"),
                Hairline: Color.Parse("#1D000000"),
                WarmOverlay: Color.Parse("#26FFF4E1")),
            UiThemePreset.Midnight => new ThemePalette(
                PageBase: Color.Parse("#151A22"),
                Surface: Color.Parse("#1E2632"),
                SurfaceAlt: Color.Parse("#293445"),
                Sidebar: Color.Parse("#0E1420"),
                SidebarAlt: Color.Parse("#182131"),
                HeroStart: Color.Parse("#27496D"),
                HeroEnd: Color.Parse("#10253C"),
                Accent: Color.Parse("#61A0FF"),
                AccentDeep: Color.Parse("#D4E4FF"),
                Highlight: Color.Parse("#7CC6FE"),
                HighlightAlt: Color.Parse("#4687D9"),
                Ink: Color.Parse("#EDF3FF"),
                MutedInk: Color.Parse("#9FB1C8"),
                SidebarText: Color.Parse("#F3F7FF"),
                SidebarMutedText: Color.Parse("#AAB9D1"),
                Hairline: Color.Parse("#30FFFFFF"),
                WarmOverlay: Color.Parse("#144D86C8")),
            UiThemePreset.Ember => new ThemePalette(
                PageBase: Color.Parse("#261915"),
                Surface: Color.Parse("#32211C"),
                SurfaceAlt: Color.Parse("#493028"),
                Sidebar: Color.Parse("#1C120F"),
                SidebarAlt: Color.Parse("#2A1B17"),
                HeroStart: Color.Parse("#9C4A32"),
                HeroEnd: Color.Parse("#57251C"),
                Accent: Color.Parse("#E48C52"),
                AccentDeep: Color.Parse("#FFE1C6"),
                Highlight: Color.Parse("#F4B15D"),
                HighlightAlt: Color.Parse("#D97A33"),
                Ink: Color.Parse("#FFF3E8"),
                MutedInk: Color.Parse("#D6B7A1"),
                SidebarText: Color.Parse("#FFF1E6"),
                SidebarMutedText: Color.Parse("#D7B3A2"),
                Hairline: Color.Parse("#38FFFFFF"),
                WarmOverlay: Color.Parse("#16FFCFB8")),
            _ => throw new ArgumentOutOfRangeException(),
        };

        ApplyPalette(resources, palette);
        ApplyShape(resources);
        ApplyDensity(resources);
        AppearanceChanged?.Invoke(this, EventArgs.Empty);
    }

    private static void ApplyPalette(IResourceDictionary resources, ThemePalette palette)
    {
        UpdateBrush(resources, "PageBaseBrush", palette.PageBase);
        UpdateBrush(resources, "SurfaceBrush", palette.Surface);
        UpdateBrush(resources, "SurfaceAltBrush", palette.SurfaceAlt);
        UpdateBrush(resources, "SidebarBrush", palette.Sidebar);
        UpdateBrush(resources, "SidebarAltBrush", palette.SidebarAlt);
        UpdateBrush(resources, "AccentBrush", palette.Accent);
        UpdateBrush(resources, "AccentDeepBrush", palette.AccentDeep);
        UpdateBrush(resources, "HighlightBrush", palette.Highlight);
        UpdateBrush(resources, "InkBrush", palette.Ink);
        UpdateBrush(resources, "MutedInkBrush", palette.MutedInk);
        UpdateBrush(resources, "SidebarTextBrush", palette.SidebarText);
        UpdateBrush(resources, "SidebarMutedTextBrush", palette.SidebarMutedText);
        UpdateBrush(resources, "HairlineBrush", palette.Hairline);
        UpdateBrush(resources, "WarmOverlayBrush", palette.WarmOverlay);

        if (resources["HeroBrush"] is LinearGradientBrush heroBrush)
        {
            heroBrush.GradientStops[0].Color = palette.HeroStart;
            heroBrush.GradientStops[1].Color = palette.HeroEnd;
        }

        if (resources["LaunchButtonBrush"] is LinearGradientBrush launchBrush)
        {
            launchBrush.GradientStops[0].Color = palette.Highlight;
            launchBrush.GradientStops[1].Color = palette.HighlightAlt;
        }
    }

    private void ApplyShape(IResourceDictionary resources)
    {
        var isRounded = SelectedShape == UiShape.Rounded;
        resources["SurfaceCardRadius"] = isRounded ? new CornerRadius(28) : new CornerRadius(16);
        resources["SoftCardRadius"] = isRounded ? new CornerRadius(22) : new CornerRadius(12);
        resources["ControlCornerRadius"] = isRounded ? new CornerRadius(16) : new CornerRadius(10);
        resources["LaunchButtonRadius"] = isRounded ? new CornerRadius(18) : new CornerRadius(10);
    }

    private void ApplyDensity(IResourceDictionary resources)
    {
        var compact = SelectedDensity == UiDensity.Compact;
        resources["SecondaryButtonPadding"] = compact ? new Thickness(12, 9) : new Thickness(16, 12);
        resources["NavButtonPadding"] = compact ? new Thickness(12, 10) : new Thickness(16, 14);
        resources["LaunchButtonPadding"] = compact ? new Thickness(20, 12) : new Thickness(26, 16);
        resources["CardInnerPadding"] = compact ? new Thickness(20) : new Thickness(28);
    }

    private static void UpdateBrush(IResourceDictionary resources, string key, Color color)
    {
        if (resources[key] is SolidColorBrush brush)
        {
            brush.Color = color;
        }
    }

    private sealed record ThemePalette(
        Color PageBase,
        Color Surface,
        Color SurfaceAlt,
        Color Sidebar,
        Color SidebarAlt,
        Color HeroStart,
        Color HeroEnd,
        Color Accent,
        Color AccentDeep,
        Color Highlight,
        Color HighlightAlt,
        Color Ink,
        Color MutedInk,
        Color SidebarText,
        Color SidebarMutedText,
        Color Hairline,
        Color WarmOverlay);
}
