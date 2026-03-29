namespace AZLauncher.Models;

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

public enum GameInstallKind
{
    Vanilla,
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

public sealed record GameInstallOption(GameInstallKind Kind, string DisplayName)
{
    public LoaderKind? LoaderKind => Kind switch
    {
        GameInstallKind.Fabric => Models.LoaderKind.Fabric,
        GameInstallKind.Forge => Models.LoaderKind.Forge,
        GameInstallKind.NeoForge => Models.LoaderKind.NeoForge,
        _ => null,
    };

    public bool RequiresLoaderVersion => LoaderKind is not null;

    public override string ToString()
    {
        return DisplayName;
    }
}

public sealed class InstanceSelectionItem
{
    public required string Id { get; init; }

    public required string Name { get; init; }

    public required string VersionSummary { get; init; }

    public required string Summary { get; init; }

    public required string SourcePath { get; init; }

    public bool IsActive { get; init; }

    public override string ToString()
    {
        return Name;
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

    public required ResourceContentType ResourceType { get; init; }

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

    public required string GameVersionId { get; init; }

    public required string ResolvedVersionId { get; init; }

    public required LoaderKind LoaderKind { get; init; }

    public required string LoaderVersion { get; init; }

    public required string InstallerPath { get; init; }

    public required string Message { get; init; }
}

public sealed class LocalResourceFileItem
{
    public required string Id { get; init; }

    public required string DisplayName { get; init; }

    public required string Summary { get; init; }

    public required string FileName { get; init; }

    public required string FullPath { get; init; }

    public required ResourceContentType ResourceType { get; init; }
}
