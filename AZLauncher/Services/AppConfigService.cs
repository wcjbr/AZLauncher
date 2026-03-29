using System;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using CommunityToolkit.Mvvm.ComponentModel;
using AZLauncher.Models;

namespace AZLauncher.Services;

public sealed class AppConfigService : ObservableObject
{
    private readonly string configDirectoryPath;
    private readonly string configFilePath;
    private readonly string defaultInstanceFolder;
    private readonly ObservableCollection<string> instanceFolders = [];
    private bool suppressPersistence;
    private AppLanguage currentLanguage;
    private UiThemePreset themePreset;
    private UiDensity density;
    private UiShape shape;
    private string launcherTitle = "AZLauncher";
    private string defaultJavaRuntime = "Java 21 Temurin";
    private int defaultMemoryGb = 6;
    private string activeInstanceFolder = string.Empty;

    public AppConfigService()
    {
        configDirectoryPath = Path.Combine(AppContext.BaseDirectory, "AZL");
        configFilePath = Path.Combine(configDirectoryPath, "config.ini");
        defaultInstanceFolder = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            "Games",
            "Minecraft");

        currentLanguage = ResolveDefaultLanguage();
        themePreset = UiThemePreset.Moss;
        density = UiDensity.Comfortable;
        shape = UiShape.Rounded;
        activeInstanceFolder = defaultInstanceFolder;

        instanceFolders.CollectionChanged += HandleInstanceFoldersChanged;

        suppressPersistence = true;
        Load();
        EnsureInstanceFolderState();
        suppressPersistence = false;
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
            var normalized = string.IsNullOrWhiteSpace(value) ? "AZLauncher" : value.Trim();
            if (SetProperty(ref launcherTitle, normalized))
            {
                Save();
            }
        }
    }

    public string DefaultJavaRuntime
    {
        get => defaultJavaRuntime;
        set
        {
            var normalized = string.IsNullOrWhiteSpace(value) ? "Java 21 Temurin" : value.Trim();
            if (SetProperty(ref defaultJavaRuntime, normalized))
            {
                Save();
            }
        }
    }

    public int DefaultMemoryGb
    {
        get => defaultMemoryGb;
        set
        {
            var normalized = Math.Clamp(value, 2, 64);
            if (SetProperty(ref defaultMemoryGb, normalized))
            {
                Save();
            }
        }
    }

    public string ActiveInstanceFolder
    {
        get => activeInstanceFolder;
        set
        {
            var normalized = NormalizeFolderPath(value) ?? defaultInstanceFolder;
            var existing = instanceFolders.FirstOrDefault(folder =>
                string.Equals(folder, normalized, StringComparison.OrdinalIgnoreCase));

            if (existing is null)
            {
                instanceFolders.Add(normalized);
                existing = normalized;
            }

            if (SetProperty(ref activeInstanceFolder, existing))
            {
                Save();
            }
        }
    }

    public ObservableCollection<string> InstanceFolders => instanceFolders;

    public string ConfigFilePath => configFilePath;

    public bool AddInstanceFolder(string folderPath)
    {
        var normalized = NormalizeFolderPath(folderPath);
        if (normalized is null)
        {
            return false;
        }

        if (instanceFolders.Any(folder => string.Equals(folder, normalized, StringComparison.OrdinalIgnoreCase)))
        {
            return false;
        }

        instanceFolders.Add(normalized);
        return true;
    }

    public bool RemoveInstanceFolder(string folderPath)
    {
        var match = instanceFolders.FirstOrDefault(folder =>
            string.Equals(folder, folderPath, StringComparison.OrdinalIgnoreCase));
        if (match is null)
        {
            return false;
        }

        return instanceFolders.Remove(match);
    }

    private void Load()
    {
        if (!File.Exists(configFilePath))
        {
            return;
        }

        var loadedFolders = new SortedDictionary<int, string>();

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
                    launcherTitle = string.IsNullOrWhiteSpace(value) ? "AZLauncher" : value;
                    break;
                case "default_java":
                    defaultJavaRuntime = string.IsNullOrWhiteSpace(value) ? "Java 21 Temurin" : value;
                    break;
                case "default_memory_gb" when int.TryParse(value, out var memoryGb):
                    defaultMemoryGb = Math.Clamp(memoryGb, 2, 64);
                    break;
                case "active_instance_folder":
                    activeInstanceFolder = NormalizeFolderPath(value) ?? defaultInstanceFolder;
                    break;
                case var keyName when keyName.StartsWith("instance_folder_", StringComparison.OrdinalIgnoreCase):
                    if (int.TryParse(keyName["instance_folder_".Length..], out var index))
                    {
                        loadedFolders[index] = value;
                    }

                    break;
            }
        }

        instanceFolders.Clear();
        foreach (var folder in loadedFolders
                     .OrderBy(entry => entry.Key)
                     .Select(entry => NormalizeFolderPath(entry.Value))
                     .Where(folder => !string.IsNullOrWhiteSpace(folder))
                     .Cast<string>()
                     .Distinct(StringComparer.OrdinalIgnoreCase))
        {
            instanceFolders.Add(folder);
        }
    }

    private void Save()
    {
        if (suppressPersistence)
        {
            return;
        }

        Directory.CreateDirectory(configDirectoryPath);

        var builder = new StringBuilder();
        builder.AppendLine("[general]");
        builder.Append("language=").AppendLine(CurrentLanguage.ToString());
        builder.Append("launcher_title=").AppendLine(LauncherTitle);
        builder.AppendLine();
        builder.AppendLine("[runtime]");
        builder.Append("default_java=").AppendLine(DefaultJavaRuntime);
        builder.Append("default_memory_gb=").AppendLine(DefaultMemoryGb.ToString());
        builder.AppendLine();
        builder.AppendLine("[instances]");
        builder.Append("active_instance_folder=").AppendLine(ActiveInstanceFolder);
        for (var index = 0; index < InstanceFolders.Count; index++)
        {
            builder.Append("instance_folder_").Append(index.ToString()).Append('=').AppendLine(InstanceFolders[index]);
        }
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

    private void HandleInstanceFoldersChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        var activeChanged = EnsureInstanceFolderState();
        OnPropertyChanged(nameof(InstanceFolders));

        if (activeChanged)
        {
            OnPropertyChanged(nameof(ActiveInstanceFolder));
        }

        Save();
    }

    private bool EnsureInstanceFolderState()
    {
        var changed = false;

        if (instanceFolders.Count == 0)
        {
            instanceFolders.Add(defaultInstanceFolder);
            changed = true;
        }

        var activeMatch = instanceFolders.FirstOrDefault(folder =>
            string.Equals(folder, activeInstanceFolder, StringComparison.OrdinalIgnoreCase));

        if (activeMatch is null)
        {
            activeInstanceFolder = instanceFolders[0];
            changed = true;
        }
        else if (!string.Equals(activeMatch, activeInstanceFolder, StringComparison.Ordinal))
        {
            activeInstanceFolder = activeMatch;
            changed = true;
        }

        return changed;
    }

    private static string? NormalizeFolderPath(string? folderPath)
    {
        if (string.IsNullOrWhiteSpace(folderPath))
        {
            return null;
        }

        try
        {
            return Path.GetFullPath(folderPath.Trim());
        }
        catch
        {
            return null;
        }
    }
}
