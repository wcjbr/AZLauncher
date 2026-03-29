namespace AZLauncher.Models;

public enum AppLanguage
{
    English,
    ChineseSimplified,
}

public enum UiThemePreset
{
    Moss,
    Midnight,
    Ember,
}

public enum UiDensity
{
    Comfortable,
    Compact,
}

public enum UiShape
{
    Rounded,
    Defined,
}

public sealed record LanguageOption(AppLanguage Language, string DisplayName)
{
    public override string ToString()
    {
        return DisplayName;
    }
}

public sealed class LauncherVersion
{
    public string Id { get; init; } = string.Empty;

    public required string Name { get; init; }

    public required string Channel { get; init; }

    public required string Summary { get; init; }

    public required string LastPlayed { get; init; }

    public bool IsRecommended { get; init; }

    public bool IsActive { get; init; }

    public bool HasBadge { get; init; }

    public string? BadgeText { get; init; }
}

public sealed class NewsItem
{
    public required string Category { get; init; }

    public required string Title { get; init; }

    public required string Summary { get; init; }
}

public sealed class LibraryItem
{
    public required string Name { get; init; }

    public required string Category { get; init; }

    public required string State { get; init; }

    public required string Summary { get; init; }

    public LibraryItemKind Kind { get; init; }
}

public enum LibraryItemKind
{
    Mod,
    ResourcePack,
    Shader,
}

public sealed class InstanceFolderItem
{
    public required string Path { get; init; }

    public required string Status { get; init; }

    public bool IsActive { get; init; }
}

public sealed class LauncherUserItem
{
    public required string Name { get; init; }

    public required string Type { get; init; }

    public required string Status { get; init; }

    public bool IsActive { get; init; }
}

public sealed class JavaRuntimeCandidate
{
    public required string DisplayName { get; init; }

    public required string ExecutablePath { get; init; }

    public required string VersionLabel { get; init; }

    public required string SourceLabel { get; init; }

    public override string ToString()
    {
        return DisplayName;
    }
}

public sealed class BackupSnapshot
{
    public required string Name { get; init; }

    public required string CreatedAt { get; init; }

    public required string Size { get; init; }

    public required string Status { get; init; }

    public required string Summary { get; init; }
}
