namespace AZLuncher.Models;

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
    public required string Name { get; init; }

    public required string Channel { get; init; }

    public required string Summary { get; init; }

    public required string LastPlayed { get; init; }

    public bool IsRecommended { get; init; }

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
}

public sealed class BackupSnapshot
{
    public required string Name { get; init; }

    public required string CreatedAt { get; init; }

    public required string Size { get; init; }

    public required string Status { get; init; }

    public required string Summary { get; init; }
}
