namespace AZLuncher.Models;

public enum DownloadSource
{
    Official,
    BMCLAPI,
}

public enum DownloadSection
{
    Games,
    Loaders,
    Resources,
}

public enum LoaderKind
{
    Fabric,
    Forge,
    NeoForge,
}

public enum ResourceContentType
{
    Mod,
    ResourcePack,
    ShaderPack,
}

public enum SearchProvider
{
    Modrinth,
    CurseForge,
}

public sealed record DownloadSourceOption(DownloadSource Source, string DisplayName)
{
    public override string ToString()
    {
        return DisplayName;
    }
}

public sealed record SearchProviderOption(SearchProvider Provider, string DisplayName)
{
    public override string ToString()
    {
        return DisplayName;
    }
}

public sealed class DownloadableLoaderVersion
{
    public required string Version { get; init; }

    public string? GameVersion { get; init; }

    public string? ExtraVersion { get; init; }

    public string DisplayName => string.IsNullOrWhiteSpace(GameVersion)
        ? Version
        : $"{GameVersion} / {Version}";

    public override string ToString()
    {
        return DisplayName;
    }
}

public sealed class SearchableResourceResult
{
    public required string Id { get; init; }

    public required SearchProvider Provider { get; init; }

    public required string Title { get; init; }

    public required string Summary { get; init; }

    public required string Author { get; init; }

    public required string VersionLabel { get; init; }

    public string? DownloadUrl { get; init; }

    public string? FileName { get; init; }

    public string? HashSha1 { get; init; }

    public string? ProjectUrl { get; init; }

    public bool IsDirectInstallAvailable => !string.IsNullOrWhiteSpace(DownloadUrl) && !string.IsNullOrWhiteSpace(FileName);
}

public sealed class LoaderInstallResult
{
    public required string ProfileId { get; init; }

    public required string InstallerPath { get; init; }

    public required string Message { get; init; }
}
