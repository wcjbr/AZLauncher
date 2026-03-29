using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using System.Xml.Linq;
using AZLauncher.Models;

namespace AZLauncher.Services;

public sealed class DownloadCenterService
{
    private static readonly HttpClient HttpClient = BuildHttpClient();
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

    public async Task<LoaderInstallResult> InstallLoaderAsync(
        LoaderKind loaderKind,
        DownloadSource source,
        string installRoot,
        string gameVersion,
        DownloadableLoaderVersion version,
        string javaRuntime,
        CancellationToken cancellationToken = default)
    {
        var installerPath = await DownloadLoaderAsync(loaderKind, source, installRoot, version, cancellationToken);
        var versionsRoot = Path.Combine(installRoot, "versions");
        var beforeSnapshot = CaptureVersionSnapshot(versionsRoot);
        var javaExecutable = ResolveJavaExecutable(javaRuntime);

        var startInfo = new ProcessStartInfo
        {
            FileName = javaExecutable,
            WorkingDirectory = installRoot,
            UseShellExecute = false,
            RedirectStandardError = true,
            RedirectStandardOutput = true,
        };

        foreach (var argument in BuildInstallerArguments(loaderKind, installerPath, installRoot, gameVersion, version.Version))
        {
            startInfo.ArgumentList.Add(argument);
        }

        using var process = Process.Start(startInfo)
            ?? throw new InvalidOperationException($"Unable to start {loaderKind} installer.");
        var stdoutTask = process.StandardOutput.ReadToEndAsync(cancellationToken);
        var stderrTask = process.StandardError.ReadToEndAsync(cancellationToken);

        await process.WaitForExitAsync(cancellationToken);
        var stdout = await stdoutTask;
        var stderr = await stderrTask;

        if (process.ExitCode != 0)
        {
            var failureDetail = string.IsNullOrWhiteSpace(stderr) ? stdout : stderr;
            throw new InvalidOperationException(
                $"{loaderKind} installer failed with exit code {process.ExitCode}: {failureDetail.Trim()}");
        }

        var resolvedVersionId = DetectInstalledVersionId(
            versionsRoot,
            beforeSnapshot,
            loaderKind,
            gameVersion,
            version.Version);

        return new LoaderInstallResult
        {
            ProfileId = resolvedVersionId,
            GameVersionId = gameVersion,
            ResolvedVersionId = resolvedVersionId,
            LoaderKind = loaderKind,
            LoaderVersion = version.Version,
            InstallerPath = installerPath,
            Message = $"{loaderKind} {version.Version} installed for Minecraft {gameVersion}.",
        };
    }

    public async Task<IReadOnlyList<SearchableResourceResult>> SearchModrinthResourcesAsync(
        ResourceContentType resourceType,
        string query,
        string? gameVersion,
        LoaderKind? loaderKind,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(query))
        {
            return [];
        }

        var facets = new List<List<string>>
        {
            new() { $"project_type:{GetProjectType(resourceType)}" },
        };

        if (!string.IsNullOrWhiteSpace(gameVersion))
        {
            facets.Add(new List<string> { $"versions:{gameVersion}" });
        }

        if (resourceType == ResourceContentType.Mod && loaderKind is not null)
        {
            facets.Add(new List<string> { $"categories:{NormalizeLoaderSlug(loaderKind.Value)}" });
        }

        var facetsJson = Uri.EscapeDataString(JsonSerializer.Serialize(facets));
        var url =
            $"https://api.modrinth.com/v2/search?query={Uri.EscapeDataString(query.Trim())}&limit=10&index=relevance&facets={facetsJson}";

        var response = await GetJsonAsync<ModrinthSearchResponse>(url, cancellationToken)
            ?? new ModrinthSearchResponse();

        var versionTasks = response.Hits
            .Where(hit => !string.IsNullOrWhiteSpace(hit.ProjectId))
            .Take(8)
            .Select(async hit =>
            {
                var version = await GetCompatibleModrinthVersionAsync(
                    hit.ProjectId!,
                    gameVersion,
                    resourceType == ResourceContentType.Mod ? loaderKind : null,
                    cancellationToken);

                var primaryFile = version?.Files?.FirstOrDefault(file => file.Primary)
                                  ?? version?.Files?.FirstOrDefault();

                return new SearchableResourceResult
                {
                    Id = hit.ProjectId!,
                    Provider = SearchProvider.Modrinth,
                    ResourceType = resourceType,
                    Title = hit.Title ?? hit.ProjectId!,
                    Summary = string.IsNullOrWhiteSpace(hit.Description)
                        ? "No description."
                        : hit.Description!,
                    Author = hit.Author ?? "Unknown",
                    VersionLabel = version?.VersionNumber
                                   ?? hit.LatestVersion
                                   ?? string.Join(", ", hit.Versions?.Take(3) ?? []),
                    DownloadUrl = primaryFile?.Url,
                    FileName = primaryFile?.Filename,
                    HashSha1 = primaryFile?.Hashes?.Sha1,
                    ProjectUrl = string.IsNullOrWhiteSpace(hit.Slug)
                        ? $"https://modrinth.com/{GetProjectType(resourceType)}/{hit.ProjectId}"
                        : $"https://modrinth.com/{GetProjectType(resourceType)}/{hit.Slug}",
                };
            });

        return await Task.WhenAll(versionTasks);
    }

    public async Task<string> DownloadResourceAsync(
        SearchableResourceResult resource,
        string installRoot,
        string instanceId,
        CancellationToken cancellationToken = default)
    {
        if (!resource.IsDirectInstallAvailable || resource.DownloadUrl is null || resource.FileName is null)
        {
            throw new InvalidOperationException("This resource does not expose a downloadable file.");
        }

        var targetPath = await DownloadResourceAsync(
            resource.ResourceType,
            resource.DownloadUrl,
            resource.FileName,
            installRoot,
            instanceId,
            cancellationToken);

        if (!string.IsNullOrWhiteSpace(resource.HashSha1))
        {
            await ValidateSha1Async(targetPath, resource.HashSha1!, cancellationToken);
        }

        return targetPath;
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

        var targetDirectory = GetResourceDirectory(installRoot, instanceId, resourceType);
        Directory.CreateDirectory(targetDirectory);

        var targetPath = Path.Combine(targetDirectory, normalizedFileName);
        await DownloadFileAsync(url, targetPath, cancellationToken);
        return targetPath;
    }

    public IReadOnlyList<LocalResourceFileItem> GetLocalResources(
        string installRoot,
        string instanceId,
        ResourceContentType resourceType,
        bool isChinese)
    {
        var resourceDirectory = GetResourceDirectory(installRoot, instanceId, resourceType);
        Directory.CreateDirectory(resourceDirectory);

        return new DirectoryInfo(resourceDirectory)
            .EnumerateFiles()
            .OrderByDescending(file => file.LastWriteTimeUtc)
            .Select(file => new LocalResourceFileItem
            {
                Id = file.FullName,
                DisplayName = Path.GetFileNameWithoutExtension(file.Name),
                Summary = BuildLocalResourceSummary(file, isChinese),
                FileName = file.Name,
                FullPath = file.FullName,
                ResourceType = resourceType,
            })
            .ToArray();
    }

    public void DeleteLocalResource(LocalResourceFileItem resource)
    {
        if (File.Exists(resource.FullPath))
        {
            File.Delete(resource.FullPath);
        }
    }

    public string GetInstanceRootPath(string installRoot, string instanceId)
    {
        return Path.Combine(installRoot, "instances", instanceId);
    }

    public string GetResourceDirectory(string installRoot, string instanceId, ResourceContentType resourceType)
    {
        return Path.Combine(
            GetInstanceRootPath(installRoot, instanceId),
            resourceType switch
            {
                ResourceContentType.Mod => "mods",
                ResourceContentType.ResourcePack => "resourcepacks",
                ResourceContentType.ShaderPack => "shaderpacks",
                _ => throw new ArgumentOutOfRangeException(nameof(resourceType)),
            });
    }

    private static HttpClient BuildHttpClient()
    {
        var client = new HttpClient();
        client.DefaultRequestHeaders.UserAgent.ParseAdd("AZLauncher/0.1.0");
        return client;
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

    private static IEnumerable<string> BuildInstallerArguments(
        LoaderKind loaderKind,
        string installerPath,
        string installRoot,
        string gameVersion,
        string loaderVersion)
    {
        if (loaderKind == LoaderKind.Fabric)
        {
            return
            [
                "-jar",
                installerPath,
                "client",
                "-dir",
                installRoot,
                "-mcversion",
                gameVersion,
                "-loader",
                loaderVersion,
                "-noprofile",
            ];
        }

        return
        [
            "-jar",
            installerPath,
            "--installClient",
            installRoot,
        ];
    }

    private static Dictionary<string, DateTimeUtcMarker> CaptureVersionSnapshot(string versionsRoot)
    {
        if (!Directory.Exists(versionsRoot))
        {
            return new Dictionary<string, DateTimeUtcMarker>(StringComparer.OrdinalIgnoreCase);
        }

        return Directory.EnumerateDirectories(versionsRoot)
            .ToDictionary(
                path => Path.GetFileName(path),
                path => new DateTimeUtcMarker(Directory.GetLastWriteTimeUtc(path)),
                StringComparer.OrdinalIgnoreCase);
    }

    private static string DetectInstalledVersionId(
        string versionsRoot,
        IReadOnlyDictionary<string, DateTimeUtcMarker> beforeSnapshot,
        LoaderKind loaderKind,
        string gameVersion,
        string loaderVersion)
    {
        Directory.CreateDirectory(versionsRoot);

        var changedDirectory = Directory.EnumerateDirectories(versionsRoot)
            .Select(path => new
            {
                Name = Path.GetFileName(path),
                LastWrite = Directory.GetLastWriteTimeUtc(path),
            })
            .Where(entry =>
            {
                return !beforeSnapshot.TryGetValue(entry.Name, out var previous)
                       || entry.LastWrite > previous.Value;
            })
            .OrderByDescending(entry => entry.LastWrite)
            .Select(entry => entry.Name)
            .FirstOrDefault();

        if (!string.IsNullOrWhiteSpace(changedDirectory))
        {
            return changedDirectory;
        }

        var preferredName = loaderKind switch
        {
            LoaderKind.Fabric => $"fabric-loader-{loaderVersion}-{gameVersion}",
            LoaderKind.Forge => $"forge-{gameVersion}-{loaderVersion}",
            LoaderKind.NeoForge => $"neoforge-{loaderVersion}",
            _ => gameVersion,
        };

        if (Directory.Exists(Path.Combine(versionsRoot, preferredName)))
        {
            return preferredName;
        }

        var heuristicMatch = Directory.EnumerateDirectories(versionsRoot)
            .Select(Path.GetFileName)
            .Where(name => !string.IsNullOrWhiteSpace(name))
            .FirstOrDefault(name =>
                name!.Contains(gameVersion, StringComparison.OrdinalIgnoreCase)
                && name.Contains(NormalizeLoaderSlug(loaderKind), StringComparison.OrdinalIgnoreCase));

        return heuristicMatch ?? gameVersion;
    }

    private static async Task<ModrinthVersion?> GetCompatibleModrinthVersionAsync(
        string projectId,
        string? gameVersion,
        LoaderKind? loaderKind,
        CancellationToken cancellationToken)
    {
        var query = new List<string>();
        if (!string.IsNullOrWhiteSpace(gameVersion))
        {
            query.Add($"game_versions={Uri.EscapeDataString(JsonSerializer.Serialize(new[] { gameVersion }))}");
        }

        if (loaderKind is not null)
        {
            query.Add(
                $"loaders={Uri.EscapeDataString(JsonSerializer.Serialize(new[] { NormalizeLoaderSlug(loaderKind.Value) }))}");
        }

        var url = $"https://api.modrinth.com/v2/project/{projectId}/version";
        if (query.Count > 0)
        {
            url += "?" + string.Join("&", query);
        }

        var versions = await GetJsonAsync<List<ModrinthVersion>>(url, cancellationToken) ?? [];
        return versions.FirstOrDefault(version => version.Files?.Any() == true);
    }

    private static string BuildLocalResourceSummary(FileInfo file, bool isChinese)
    {
        var size = FormatBytes(file.Length);
        var updated = file.LastWriteTime.ToString("yyyy-MM-dd HH:mm");
        return isChinese
            ? $"{file.Extension} · {size} · 更新于 {updated}"
            : $"{file.Extension} · {size} · Updated {updated}";
    }

    private static string FormatBytes(long bytes)
    {
        string[] units = ["B", "KB", "MB", "GB"];
        double size = bytes;
        var unitIndex = 0;

        while (size >= 1024 && unitIndex < units.Length - 1)
        {
            size /= 1024;
            unitIndex++;
        }

        return size >= 100
            ? $"{size:F0} {units[unitIndex]}"
            : $"{size:F1} {units[unitIndex]}";
    }

    private static string GetProjectType(ResourceContentType resourceType)
    {
        return resourceType switch
        {
            ResourceContentType.Mod => "mod",
            ResourceContentType.ResourcePack => "resourcepack",
            ResourceContentType.ShaderPack => "shader",
            _ => throw new ArgumentOutOfRangeException(nameof(resourceType)),
        };
    }

    private static string NormalizeLoaderSlug(LoaderKind loaderKind)
    {
        return loaderKind switch
        {
            LoaderKind.Fabric => "fabric",
            LoaderKind.Forge => "forge",
            LoaderKind.NeoForge => "neoforge",
            _ => loaderKind.ToString().ToLowerInvariant(),
        };
    }

    private static string ResolveJavaExecutable(string javaRuntime)
    {
        if (!string.IsNullOrWhiteSpace(javaRuntime))
        {
            if (File.Exists(javaRuntime))
            {
                return javaRuntime;
            }

            var binJava = Path.Combine(javaRuntime, "bin", OperatingSystem.IsWindows() ? "java.exe" : "java");
            if (File.Exists(binJava))
            {
                return binJava;
            }
        }

        return OperatingSystem.IsWindows() ? "java.exe" : "java";
    }

    private static async Task ValidateSha1Async(string filePath, string expectedSha1, CancellationToken cancellationToken)
    {
        await using var stream = File.OpenRead(filePath);
        var actualBytes = await SHA1.HashDataAsync(stream, cancellationToken);
        var actual = Convert.ToHexString(actualBytes).ToLowerInvariant();
        if (!string.Equals(actual, expectedSha1.Trim(), StringComparison.OrdinalIgnoreCase))
        {
            File.Delete(filePath);
            throw new InvalidOperationException("Downloaded file hash did not match Modrinth metadata.");
        }
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
        Directory.CreateDirectory(Path.GetDirectoryName(targetPath)!);
        await using var source = await HttpClient.GetStreamAsync(url, cancellationToken);
        await using var target = File.Create(targetPath);
        await source.CopyToAsync(target, cancellationToken);
    }

    private sealed record DateTimeUtcMarker(DateTime Value);

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

    private sealed class ModrinthSearchResponse
    {
        public List<ModrinthSearchHit> Hits { get; init; } = [];
    }

    private sealed class ModrinthSearchHit
    {
        [JsonPropertyName("project_id")]
        public string? ProjectId { get; init; }

        public string? Slug { get; init; }

        public string? Title { get; init; }

        public string? Description { get; init; }

        public string? Author { get; init; }

        [JsonPropertyName("latest_version")]
        public string? LatestVersion { get; init; }

        public List<string>? Versions { get; init; }
    }

    private sealed class ModrinthVersion
    {
        [JsonPropertyName("version_number")]
        public string? VersionNumber { get; init; }

        public List<ModrinthFile>? Files { get; init; }
    }

    private sealed class ModrinthFile
    {
        public string? Url { get; init; }

        public string? Filename { get; init; }

        public bool Primary { get; init; }

        public ModrinthHashes? Hashes { get; init; }
    }

    private sealed class ModrinthHashes
    {
        public string? Sha1 { get; init; }
    }
}
