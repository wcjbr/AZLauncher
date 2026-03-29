using System;
using System.IO;
using System.Text;
using CommunityToolkit.Mvvm.ComponentModel;
using AZLuncher.Models;

namespace AZLuncher.Services;

public sealed class AppConfigService : ObservableObject
{
    private readonly string configDirectoryPath;
    private readonly string configFilePath;
    private AppLanguage currentLanguage;
    private UiThemePreset themePreset;
    private UiDensity density;
    private UiShape shape;
    private string launcherTitle = "AZLuncher";

    public AppConfigService()
    {
        configDirectoryPath = Path.Combine(AppContext.BaseDirectory, "AZL");
        configFilePath = Path.Combine(configDirectoryPath, "config.ini");

        currentLanguage = ResolveDefaultLanguage();
        themePreset = UiThemePreset.Moss;
        density = UiDensity.Comfortable;
        shape = UiShape.Rounded;

        Load();
        Save();
    }

    public AppLanguage CurrentLanguage
    {
        get => currentLanguage;
        set
        {
            if (SetProperty(ref currentLanguage, value))
            {
                Save();
            }
        }
    }

    public UiThemePreset ThemePreset
    {
        get => themePreset;
        set
        {
            if (SetProperty(ref themePreset, value))
            {
                Save();
            }
        }
    }

    public UiDensity Density
    {
        get => density;
        set
        {
            if (SetProperty(ref density, value))
            {
                Save();
            }
        }
    }

    public UiShape Shape
    {
        get => shape;
        set
        {
            if (SetProperty(ref shape, value))
            {
                Save();
            }
        }
    }

    public string LauncherTitle
    {
        get => launcherTitle;
        set
        {
            var normalized = string.IsNullOrWhiteSpace(value) ? "AZLuncher" : value.Trim();
            if (SetProperty(ref launcherTitle, normalized))
            {
                Save();
            }
        }
    }

    public string ConfigFilePath => configFilePath;

    private void Load()
    {
        if (!File.Exists(configFilePath))
        {
            return;
        }

        foreach (var rawLine in File.ReadAllLines(configFilePath))
        {
            var line = rawLine.Trim();
            if (string.IsNullOrEmpty(line) || line.StartsWith('#') || line.StartsWith(';') || line.StartsWith('['))
            {
                continue;
            }

            var separatorIndex = line.IndexOf('=');
            if (separatorIndex <= 0)
            {
                continue;
            }

            var key = line[..separatorIndex].Trim();
            var value = line[(separatorIndex + 1)..].Trim();

            switch (key)
            {
                case "language" when Enum.TryParse<AppLanguage>(value, true, out var language):
                    currentLanguage = language;
                    break;
                case "theme_preset" when Enum.TryParse<UiThemePreset>(value, true, out var preset):
                    themePreset = preset;
                    break;
                case "density" when Enum.TryParse<UiDensity>(value, true, out var uiDensity):
                    density = uiDensity;
                    break;
                case "shape" when Enum.TryParse<UiShape>(value, true, out var uiShape):
                    shape = uiShape;
                    break;
                case "launcher_title":
                    launcherTitle = string.IsNullOrWhiteSpace(value) ? "AZLuncher" : value;
                    break;
            }
        }
    }

    private void Save()
    {
        Directory.CreateDirectory(configDirectoryPath);

        var builder = new StringBuilder();
        builder.AppendLine("[general]");
        builder.Append("language=").AppendLine(CurrentLanguage.ToString());
        builder.Append("launcher_title=").AppendLine(LauncherTitle);
        builder.AppendLine();
        builder.AppendLine("[appearance]");
        builder.Append("theme_preset=").AppendLine(ThemePreset.ToString());
        builder.Append("density=").AppendLine(Density.ToString());
        builder.Append("shape=").AppendLine(Shape.ToString());

        File.WriteAllText(configFilePath, builder.ToString(), new UTF8Encoding(false));
    }

    private static AppLanguage ResolveDefaultLanguage()
    {
        return System.Globalization.CultureInfo.CurrentUICulture.Name.StartsWith("zh", StringComparison.OrdinalIgnoreCase)
            ? AppLanguage.ChineseSimplified
            : AppLanguage.English;
    }
}
