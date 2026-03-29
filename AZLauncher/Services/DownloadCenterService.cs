using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using System.Xml.Linq;
using AZLauncher.Models;

namespace AZLauncher.Services;

public sealed class DownloadCenterService
{
    private static readonly HttpClient HttpClient = new();
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    private readonly MinecraftRuntimeService minecraftRuntimeService = new();

    public Task<IReadOnlyList<DownloadableGameVersion>> GetGameVersionsAsync(
        string installRoot,
        DownloadSource source,
        CancellationToken cancellationToken = default)
    {
        return minecraftRuntimeService.GetAvailableVersionsAsync(installRoot, source, cancellationToken);
    }

    public Task DownloadGameAsync(
        string versionId,
        string installRoot,
        DownloadSource source,
        IProgress<RuntimeProgressUpdate>? progress = null,
        CancellationToken cancellationToken = default)
    {
        return minecraftRuntimeService.DownloadVersionAsync(versionId, installRoot, source, progress, cancellationToken);
    }

    public async Task<IReadOnlyList<DownloadableLoaderVersion>> GetLoaderVersionsAsync(
        LoaderKind loaderKind,
        DownloadSource source,
        string? gameVersion = null,
        CancellationToken cancellationToken = default)
    {
        return loaderKind switch
        {
            LoaderKind.Fabric => await GetFabricVersionsAsync(source, gameVersion, cancellationToken),
            LoaderKind.Forge => await GetForgeVersionsAsync(source, cancellationToken),
            LoaderKind.NeoForge => await GetNeoForgeVersionsAsync(source, cancellationToken),
            _ => [],
        };
    }

    public async Task<string> DownloadLoaderAsync(
        LoaderKind loaderKind,
        DownloadSource source,
        string installRoot,
        DownloadableLoaderVersion version,
        CancellationToken cancellationToken = default)
    {
        var loadersRoot = Path.Combine(installRoot, "loaders", loaderKind.ToString().ToLowerInvariant());
        Directory.CreateDirectory(loadersRoot);

        return loaderKind switch
        {
            LoaderKind.Fabric => await DownloadFabricInstallerAsync(loadersRoot, source, version, cancellationToken),
            LoaderKind.Forge => await DownloadForgeInstallerAsync(loadersRoot, source, version, cancellationToken),
            LoaderKind.NeoForge => await DownloadNeoForgeInstallerAsync(loadersRoot, source, version, cancellationToken),
            _ => throw new ArgumentOutOfRangeException(nameof(loaderKind)),
        };
    }

    public async Task<string> DownloadResourceAsync(
        ResourceContentType resourceType,
        string url,
        string fileName,
        string installRoot,
        string instanceId,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(url))
        {
            throw new InvalidOperationException("Resource URL is required.");
        }

        var normalizedFileName = string.IsNullOrWhiteSpace(fileName)
            ? Path.GetFileName(new Uri(url).LocalPath)
            : fileName.Trim();

        if (string.IsNullOrWhiteSpace(normalizedFileName))
        {
            throw new InvalidOperationException("Resource file name is required.");
        }

        var targetDirectory = Path.Combine(
            installRoot,
            "instances",
            instanceId,
            resourceType switch
            {
                ResourceContentType.Mod => "mods",
                ResourceContentType.ResourcePack => "resourcepacks",
                ResourceContentType.ShaderPack => "shaderpacks",
                _ => throw new ArgumentOutOfRangeException(nameof(resourceType)),
            });

        Directory.CreateDirectory(targetDirectory);
        var targetPath = Path.Combine(targetDirectory, normalizedFileName);
        await DownloadFileAsync(url, targetPath, cancellationToken);
        return targetPath;
    }

    private static async Task<IReadOnlyList<DownloadableLoaderVersion>> GetForgeVersionsAsync(
        DownloadSource source,
        CancellationToken cancellationToken)
    {
        var metadataUrl = source == DownloadSource.BMCLAPI
            ? "https://bmclapi2.bangbang93.com/maven/net/minecraftforge/forge/maven-metadata.xml"
            : "https://maven.minecraftforge.net/net/minecraftforge/forge/maven-metadata.xml";

        var versions = await GetMavenVersionsAsync(metadataUrl, cancellationToken);
        return versions
            .TakeLast(60)
            .Reverse()
            .Select(version => new DownloadableLoaderVersion
            {
                Version = version,
                GameVersion = version.Split('-', 2)[0],
            })
            .ToArray();
    }

    private static async Task<IReadOnlyList<DownloadableLoaderVersion>> GetNeoForgeVersionsAsync(
        DownloadSource source,
        CancellationToken cancellationToken)
    {
        var metadataUrl = source == DownloadSource.BMCLAPI
            ? "https://bmclapi2.bangbang93.com/maven/net/neoforged/neoforge/maven-metadata.xml"
            : "https://maven.neoforged.net/releases/net/neoforged/neoforge/maven-metadata.xml";

        var versions = await GetMavenVersionsAsync(metadataUrl, cancellationToken);
        return versions
            .TakeLast(60)
            .Reverse()
            .Select(version => new DownloadableLoaderVersion
            {
                Version = version,
                GameVersion = version.Split('-', 2)[0],
            })
            .ToArray();
    }

    private static async Task<IReadOnlyList<DownloadableLoaderVersion>> GetFabricVersionsAsync(
        DownloadSource source,
        string? gameVersion,
        CancellationToken cancellationToken)
    {
        var loaderApiUrl = string.IsNullOrWhiteSpace(gameVersion)
            ? "https://meta.fabricmc.net/v2/versions/loader"
            : $"https://meta.fabricmc.net/v2/versions/loader/{gameVersion}";

        var entries = await GetJsonAsync<List<FabricLoaderEntry>>(loaderApiUrl, cancellationToken) ?? [];

        return entries
            .Select(entry => new DownloadableLoaderVersion
            {
                GameVersion = entry.Intermediary?.Version ?? gameVersion ?? string.Empty,
                Version = entry.Loader?.Version ?? string.Empty,
                ExtraVersion = entry.Installer?.Version ?? string.Empty,
            })
            .Where(entry => !string.IsNullOrWhiteSpace(entry.Version))
            .Take(60)
            .ToArray();
    }

    private static async Task<string> DownloadForgeInstallerAsync(
        string loadersRoot,
        DownloadSource source,
        DownloadableLoaderVersion version,
        CancellationToken cancellationToken)
    {
        var baseUrl = source == DownloadSource.BMCLAPI
            ? "https://bmclapi2.bangbang93.com/maven/net/minecraftforge/forge"
            : "https://maven.minecraftforge.net/net/minecraftforge/forge";

        var fileName = $"forge-{version.Version}-installer.jar";
        var targetPath = Path.Combine(loadersRoot, fileName);
        var url = $"{baseUrl}/{version.Version}/{fileName}";
        await DownloadFileAsync(url, targetPath, cancellationToken);
        return targetPath;
    }

    private static async Task<string> DownloadNeoForgeInstallerAsync(
        string loadersRoot,
        DownloadSource source,
        DownloadableLoaderVersion version,
        CancellationToken cancellationToken)
    {
        var baseUrl = source == DownloadSource.BMCLAPI
            ? "https://bmclapi2.bangbang93.com/maven/net/neoforged/neoforge"
            : "https://maven.neoforged.net/releases/net/neoforged/neoforge";

        var fileName = $"neoforge-{version.Version}-installer.jar";
        var targetPath = Path.Combine(loadersRoot, fileName);
        var url = $"{baseUrl}/{version.Version}/{fileName}";
        await DownloadFileAsync(url, targetPath, cancellationToken);
        return targetPath;
    }

    private static async Task<string> DownloadFabricInstallerAsync(
        string loadersRoot,
        DownloadSource source,
        DownloadableLoaderVersion version,
        CancellationToken cancellationToken)
    {
        var installerVersion = !string.IsNullOrWhiteSpace(version.ExtraVersion)
            ? version.ExtraVersion
            : await GetLatestFabricInstallerVersionAsync(cancellationToken);

        var baseUrl = source == DownloadSource.BMCLAPI
            ? "https://bmclapi2.bangbang93.com/maven/net/fabricmc/fabric-installer"
            : "https://maven.fabricmc.net/net/fabricmc/fabric-installer";

        var fileName = $"fabric-installer-{installerVersion}.jar";
        var targetPath = Path.Combine(loadersRoot, $"fabric-{version.GameVersion}-{version.Version}-installer.jar");
        var url = $"{baseUrl}/{installerVersion}/{fileName}";
        await DownloadFileAsync(url, targetPath, cancellationToken);
        return targetPath;
    }

    private static async Task<string> GetLatestFabricInstallerVersionAsync(CancellationToken cancellationToken)
    {
        var installers = await GetJsonAsync<List<FabricInstallerEntry>>("https://meta.fabricmc.net/v2/versions/installer", cancellationToken) ?? [];
        return installers.FirstOrDefault(entry => entry.Stable)?.Version
               ?? installers.FirstOrDefault()?.Version
               ?? throw new InvalidOperationException("Fabric installer version is unavailable.");
    }

    private static async Task<List<string>> GetMavenVersionsAsync(string metadataUrl, CancellationToken cancellationToken)
    {
        await using var stream = await HttpClient.GetStreamAsync(metadataUrl, cancellationToken);
        var document = XDocument.Load(stream);
        return document
            .Descendants("version")
            .Select(element => element.Value.Trim())
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .ToList();
    }

    private static async Task<T?> GetJsonAsync<T>(string url, CancellationToken cancellationToken)
    {
        await using var stream = await HttpClient.GetStreamAsync(url, cancellationToken);
        return await JsonSerializer.DeserializeAsync<T>(stream, JsonOptions, cancellationToken);
    }

    private static async Task DownloadFileAsync(string url, string targetPath, CancellationToken cancellationToken)
    {
        if (File.Exists(targetPath))
        {
            return;
        }

        Directory.CreateDirectory(Path.GetDirectoryName(targetPath)!);
        await using var source = await HttpClient.GetStreamAsync(url, cancellationToken);
        await using var target = File.Create(targetPath);
        await source.CopyToAsync(target, cancellationToken);
    }

    private sealed class FabricLoaderEntry
    {
        public FabricVersionInfo? Loader { get; init; }

        public FabricVersionInfo? Intermediary { get; init; }

        public FabricVersionInfo? Installer { get; init; }
    }

    private sealed class FabricInstallerEntry
    {
        public string Version { get; init; } = string.Empty;

        public bool Stable { get; init; }
    }

    private sealed class FabricVersionInfo
    {
        public string? Version { get; init; }
    }
}
