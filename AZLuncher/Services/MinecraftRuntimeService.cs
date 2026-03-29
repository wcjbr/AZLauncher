using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net.Http;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using AZLuncher.Models;

namespace AZLuncher.Services;

public sealed class MinecraftRuntimeService
{
    private static readonly Uri VersionManifestUri = new("https://piston-meta.mojang.com/mc/game/version_manifest_v2.json");
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    private static readonly HttpClient HttpClient = new();

    public async Task<IReadOnlyList<DownloadableGameVersion>> GetAvailableVersionsAsync(
        string minecraftRoot,
        DownloadSource source = DownloadSource.Official,
        CancellationToken cancellationToken = default)
    {
        var manifest = await GetManifestAsync(source, cancellationToken);
        return manifest.Versions
            .Where(version => !string.IsNullOrWhiteSpace(version.Id))
            .Take(40)
            .Select(version => new DownloadableGameVersion
            {
                Id = version.Id!,
                Type = version.Type ?? "release",
                ReleaseTime = version.ReleaseTime?.ToString("yyyy-MM-dd") ?? string.Empty,
                IsInstalled = IsVersionInstalled(minecraftRoot, version.Id!),
            })
            .ToArray();
    }

    public async Task DownloadVersionAsync(
        string versionId,
        string minecraftRoot,
        DownloadSource source = DownloadSource.Official,
        IProgress<RuntimeProgressUpdate>? progress = null,
        CancellationToken cancellationToken = default)
    {
        var manifest = await GetManifestAsync(source, cancellationToken);
        var manifestEntry = manifest.Versions.FirstOrDefault(version => string.Equals(version.Id, versionId, StringComparison.Ordinal));
        if (manifestEntry?.Url is null)
        {
            throw new InvalidOperationException($"Version '{versionId}' was not found in the official manifest.");
        }

        progress?.Report(new RuntimeProgressUpdate { Progress = 2, Message = $"Fetching metadata for {versionId}" });

        var metadata = await GetJsonAsync<VersionMetadata>(RewriteUrl(manifestEntry.Url, source), cancellationToken)
            ?? throw new InvalidOperationException($"Version metadata for '{versionId}' is unavailable.");

        if (metadata.Downloads?.Client?.Url is null)
        {
            throw new InvalidOperationException($"Version '{versionId}' does not expose an official client download.");
        }

        var versionRoot = Path.Combine(minecraftRoot, "versions", versionId);
        var librariesRoot = Path.Combine(minecraftRoot, "libraries");
        var assetsRoot = Path.Combine(minecraftRoot, "assets");
        var indexesRoot = Path.Combine(assetsRoot, "indexes");
        var objectsRoot = Path.Combine(assetsRoot, "objects");
        var nativesRoot = Path.Combine(versionRoot, "natives");

        Directory.CreateDirectory(versionRoot);
        Directory.CreateDirectory(librariesRoot);
        Directory.CreateDirectory(indexesRoot);
        Directory.CreateDirectory(objectsRoot);
        Directory.CreateDirectory(nativesRoot);

        var versionJsonPath = Path.Combine(versionRoot, $"{versionId}.json");
        var clientJarPath = Path.Combine(versionRoot, $"{versionId}.jar");

        await DownloadFileAsync(RewriteUrl(metadata.Downloads.Client.Url, source), clientJarPath, cancellationToken);
        await File.WriteAllTextAsync(versionJsonPath, JsonSerializer.Serialize(metadata, JsonOptions), cancellationToken);

        progress?.Report(new RuntimeProgressUpdate { Progress = 10, Message = $"Downloading libraries for {versionId}" });

        var libraries = ResolveAllowedLibraries(metadata);
        var totalSteps = Math.Max(1, libraries.Count + 3);
        var currentStep = 1;

        foreach (var library in libraries)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (library.Downloads?.Artifact?.Url is not null && library.Downloads.Artifact.Path is not null)
            {
                var artifactPath = Path.Combine(librariesRoot, library.Downloads.Artifact.Path);
                await DownloadFileAsync(RewriteUrl(library.Downloads.Artifact.Url, source), artifactPath, cancellationToken);
            }

            var nativeDownload = ResolveNativeDownload(library);
            if (nativeDownload?.Url is not null && nativeDownload.Path is not null)
            {
                var archivePath = Path.Combine(librariesRoot, nativeDownload.Path);
                await DownloadFileAsync(RewriteUrl(nativeDownload.Url, source), archivePath, cancellationToken);
                ExtractNativeArchive(archivePath, nativesRoot, library.Extract?.Exclude);
            }

            currentStep++;
            progress?.Report(new RuntimeProgressUpdate
            {
                Progress = 10 + (80d * currentStep / totalSteps),
                Message = $"Resolved library {library.Name}",
            });
        }

        if (metadata.AssetIndex?.Url is not null && !string.IsNullOrWhiteSpace(metadata.AssetIndex.Id))
        {
            var assetIndexPath = Path.Combine(indexesRoot, $"{metadata.AssetIndex.Id}.json");
            await DownloadFileAsync(RewriteUrl(metadata.AssetIndex.Url, source), assetIndexPath, cancellationToken);

            var assetIndex = await JsonSerializer.DeserializeAsync<AssetIndex>(
                File.OpenRead(assetIndexPath),
                JsonOptions,
                cancellationToken);

            if (assetIndex?.Objects is not null)
            {
                var assets = assetIndex.Objects.Values.ToArray();
                for (var index = 0; index < assets.Length; index++)
                {
                    var asset = assets[index];
                    if (string.IsNullOrWhiteSpace(asset.Hash) || asset.Hash.Length < 2)
                    {
                        continue;
                    }

                    var objectPath = Path.Combine(objectsRoot, asset.Hash[..2], asset.Hash);
                    var objectUrl = RewriteUrl($"https://resources.download.minecraft.net/{asset.Hash[..2]}/{asset.Hash}", source);
                    await DownloadFileAsync(objectUrl, objectPath, cancellationToken);

                    progress?.Report(new RuntimeProgressUpdate
                    {
                        Progress = 90 + (9d * (index + 1) / Math.Max(1, assets.Length)),
                        Message = $"Downloaded asset {index + 1}/{assets.Length}",
                    });
                }
            }
        }

        progress?.Report(new RuntimeProgressUpdate { Progress = 100, Message = $"Finished downloading {versionId}" });
    }

    public async Task<LaunchExecutionResult> LaunchAsync(
        string versionId,
        string minecraftRoot,
        string gameDirectory,
        string javaRuntime,
        int memoryGb,
        string playerName,
        CancellationToken cancellationToken = default)
    {
        var versionRoot = Path.Combine(minecraftRoot, "versions", versionId);
        var versionJsonPath = Path.Combine(versionRoot, $"{versionId}.json");
        var versionJarPath = Path.Combine(versionRoot, $"{versionId}.jar");
        var librariesRoot = Path.Combine(minecraftRoot, "libraries");
        var assetsRoot = Path.Combine(minecraftRoot, "assets");
        var nativesRoot = Path.Combine(versionRoot, "natives");

        if (!File.Exists(versionJsonPath) || !File.Exists(versionJarPath))
        {
            return new LaunchExecutionResult
            {
                Started = false,
                CommandPreview = string.Empty,
                Message = $"Version {versionId} is not installed locally yet.",
            };
        }

        await using var versionStream = File.OpenRead(versionJsonPath);
        var metadata = await JsonSerializer.DeserializeAsync<VersionMetadata>(versionStream, JsonOptions, cancellationToken)
            ?? throw new InvalidOperationException($"Unable to load local metadata for {versionId}.");

        var libraries = ResolveAllowedLibraries(metadata)
            .Where(library => library.Downloads?.Artifact?.Path is not null)
            .Select(library => Path.Combine(librariesRoot, library.Downloads!.Artifact!.Path!))
            .Where(File.Exists)
            .ToList();

        libraries.Add(versionJarPath);

        Directory.CreateDirectory(gameDirectory);

        var classpath = string.Join(Path.PathSeparator, libraries);
        var assetIndexName = metadata.AssetIndex?.Id ?? metadata.Assets ?? "legacy";
        var versionType = metadata.Type ?? "release";
        var javaExecutable = ResolveJavaExecutable(javaRuntime);
        var offlineUuid = BuildOfflineUuid(playerName);

        var substitutions = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["auth_player_name"] = playerName,
            ["version_name"] = versionId,
            ["game_directory"] = gameDirectory,
            ["assets_root"] = assetsRoot,
            ["assets_index_name"] = assetIndexName,
            ["auth_uuid"] = offlineUuid,
            ["auth_access_token"] = "0",
            ["user_type"] = "legacy",
            ["version_type"] = versionType,
            ["user_properties"] = "{}",
            ["auth_session"] = "0",
            ["game_assets"] = Path.Combine(assetsRoot, "virtual", "legacy"),
            ["clientid"] = "AZLauncher",
            ["xuid"] = "0",
            ["launcher_name"] = "AZLauncher",
            ["launcher_version"] = "0.1.0",
            ["classpath"] = classpath,
            ["classpath_separator"] = Path.PathSeparator.ToString(),
            ["natives_directory"] = nativesRoot,
            ["library_directory"] = librariesRoot,
            ["resolution_width"] = "1280",
            ["resolution_height"] = "720",
        };

        var arguments = new List<string> { $"-Xmx{Math.Max(2, memoryGb)}G" };
        arguments.AddRange(BuildJvmArguments(metadata, substitutions, classpath, nativesRoot, librariesRoot));
        arguments.Add(metadata.MainClass ?? "net.minecraft.client.main.Main");
        arguments.AddRange(BuildGameArguments(metadata, substitutions));

        var preview = $"{javaExecutable} {string.Join(' ', arguments.Select(QuoteArgument))}";

        var startInfo = new ProcessStartInfo
        {
            FileName = javaExecutable,
            WorkingDirectory = gameDirectory,
            UseShellExecute = false,
        };

        foreach (var argument in arguments)
        {
            startInfo.ArgumentList.Add(argument);
        }

        var process = Process.Start(startInfo);
        if (process is null)
        {
            return new LaunchExecutionResult
            {
                Started = false,
                CommandPreview = preview,
                Message = $"Failed to start Java for version {versionId}.",
            };
        }

        return new LaunchExecutionResult
        {
            Started = true,
            ProcessId = process.Id,
            CommandPreview = preview,
            Message = $"Started version {versionId} with process {process.Id}.",
        };
    }

    public bool IsVersionInstalled(string minecraftRoot, string versionId)
    {
        var versionRoot = Path.Combine(minecraftRoot, "versions", versionId);
        return File.Exists(Path.Combine(versionRoot, $"{versionId}.json"))
               && File.Exists(Path.Combine(versionRoot, $"{versionId}.jar"));
    }

    private static async Task<VersionManifest> GetManifestAsync(DownloadSource source, CancellationToken cancellationToken)
    {
        return await GetJsonAsync<VersionManifest>(GetVersionManifestUri(source), cancellationToken)
               ?? throw new InvalidOperationException("Official Minecraft version manifest is unavailable.");
    }

    private static async Task<T?> GetJsonAsync<T>(Uri uri, CancellationToken cancellationToken)
    {
        await using var stream = await HttpClient.GetStreamAsync(uri, cancellationToken);
        return await JsonSerializer.DeserializeAsync<T>(stream, JsonOptions, cancellationToken);
    }

    private static Task<T?> GetJsonAsync<T>(string uri, CancellationToken cancellationToken)
    {
        return GetJsonAsync<T>(new Uri(uri), cancellationToken);
    }

    private static Uri GetVersionManifestUri(DownloadSource source)
    {
        return source == DownloadSource.BMCLAPI
            ? new Uri("https://bmclapi2.bangbang93.com/mc/game/version_manifest_v2.json")
            : VersionManifestUri;
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

    private static List<LibraryInfo> ResolveAllowedLibraries(VersionMetadata metadata)
    {
        return (metadata.Libraries ?? [])
            .Where(IsLibraryAllowed)
            .ToList();
    }

    private static bool IsLibraryAllowed(LibraryInfo library)
    {
        if (library.Rules is null || library.Rules.Count == 0)
        {
            return true;
        }

        var allowed = false;
        foreach (var rule in library.Rules)
        {
            if (!DoesRuleMatch(rule))
            {
                continue;
            }

            allowed = string.Equals(rule.Action, "allow", StringComparison.OrdinalIgnoreCase);
        }

        return allowed;
    }

    private static bool DoesRuleMatch(RuleInfo rule)
    {
        if (rule.Features?.Count > 0)
        {
            return false;
        }

        if (rule.Os is null)
        {
            return true;
        }

        if (!string.IsNullOrWhiteSpace(rule.Os.Name) &&
            !string.Equals(rule.Os.Name, GetMinecraftOsName(), StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        if (!string.IsNullOrWhiteSpace(rule.Os.Arch) &&
            !string.Equals(rule.Os.Arch, RuntimeInformation.OSArchitecture.ToString(), StringComparison.OrdinalIgnoreCase) &&
            !string.Equals(rule.Os.Arch, Environment.Is64BitOperatingSystem ? "x86_64" : "x86", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        if (!string.IsNullOrWhiteSpace(rule.Os.Version))
        {
            var versionString = Environment.OSVersion.VersionString;
            if (!Regex.IsMatch(versionString, rule.Os.Version))
            {
                return false;
            }
        }

        return true;
    }

    private static LibraryDownloadInfo? ResolveNativeDownload(LibraryInfo library)
    {
        if (library.Downloads?.Classifiers is null || library.Natives is null)
        {
            return null;
        }

        if (!library.Natives.TryGetValue(GetMinecraftOsName(), out var classifierTemplate) ||
            string.IsNullOrWhiteSpace(classifierTemplate))
        {
            return null;
        }

        var classifier = classifierTemplate.Replace("${arch}", Environment.Is64BitOperatingSystem ? "64" : "32", StringComparison.Ordinal);
        return library.Downloads.Classifiers.TryGetValue(classifier, out var info) ? info : null;
    }

    private static void ExtractNativeArchive(string archivePath, string destinationRoot, IReadOnlyList<string>? excludes)
    {
        using var archive = ZipFile.OpenRead(archivePath);
        foreach (var entry in archive.Entries)
        {
            if (string.IsNullOrEmpty(entry.Name))
            {
                continue;
            }

            if (excludes is not null && excludes.Any(exclude => entry.FullName.StartsWith(exclude, StringComparison.OrdinalIgnoreCase)))
            {
                continue;
            }

            var destinationPath = Path.Combine(destinationRoot, entry.FullName.Replace('/', Path.DirectorySeparatorChar));
            Directory.CreateDirectory(Path.GetDirectoryName(destinationPath)!);
            entry.ExtractToFile(destinationPath, overwrite: true);
        }
    }

    private static IEnumerable<string> BuildJvmArguments(
        VersionMetadata metadata,
        IReadOnlyDictionary<string, string> substitutions,
        string classpath,
        string nativesRoot,
        string librariesRoot)
    {
        if (metadata.Arguments?.Jvm?.Count > 0)
        {
            return ExpandArgumentEntries(metadata.Arguments.Jvm, substitutions);
        }

        return
        [
            $"-Djava.library.path={nativesRoot}",
            "-cp",
            classpath,
        ];
    }

    private static IEnumerable<string> BuildGameArguments(
        VersionMetadata metadata,
        IReadOnlyDictionary<string, string> substitutions)
    {
        if (metadata.Arguments?.Game?.Count > 0)
        {
            return ExpandArgumentEntries(metadata.Arguments.Game, substitutions);
        }

        if (!string.IsNullOrWhiteSpace(metadata.MinecraftArguments))
        {
            return ReplacePlaceholders(metadata.MinecraftArguments, substitutions)
                .Split(' ', StringSplitOptions.RemoveEmptyEntries);
        }

        return [];
    }

    private static IEnumerable<string> ExpandArgumentEntries(
        IReadOnlyList<JsonElement> entries,
        IReadOnlyDictionary<string, string> substitutions)
    {
        var results = new List<string>();

        foreach (var entry in entries)
        {
            if (entry.ValueKind == JsonValueKind.String)
            {
                results.Add(ReplacePlaceholders(entry.GetString() ?? string.Empty, substitutions));
                continue;
            }

            if (entry.ValueKind != JsonValueKind.Object)
            {
                continue;
            }

            var rules = entry.TryGetProperty("rules", out var rulesElement)
                ? JsonSerializer.Deserialize<List<RuleInfo>>(rulesElement.GetRawText(), JsonOptions)
                : null;

            if (rules?.Count > 0 && !rules.Any(DoesRuleMatch))
            {
                continue;
            }

            if (!entry.TryGetProperty("value", out var valueElement))
            {
                continue;
            }

            if (valueElement.ValueKind == JsonValueKind.String)
            {
                results.Add(ReplacePlaceholders(valueElement.GetString() ?? string.Empty, substitutions));
                continue;
            }

            if (valueElement.ValueKind != JsonValueKind.Array)
            {
                continue;
            }

            results.AddRange(valueElement
                .EnumerateArray()
                .Where(element => element.ValueKind == JsonValueKind.String)
                .Select(element => ReplacePlaceholders(element.GetString() ?? string.Empty, substitutions)));
        }

        return results;
    }

    private static string ReplacePlaceholders(string value, IReadOnlyDictionary<string, string> substitutions)
    {
        var result = value;
        foreach (var pair in substitutions)
        {
            result = result.Replace("${" + pair.Key + "}", pair.Value, StringComparison.Ordinal);
        }

        return result;
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

    private static string BuildOfflineUuid(string playerName)
    {
        using var md5 = MD5.Create();
        var hash = md5.ComputeHash(Encoding.UTF8.GetBytes("OfflinePlayer:" + playerName));
        return Convert.ToHexString(hash).ToLowerInvariant();
    }

    private static string QuoteArgument(string value)
    {
        return value.Contains(' ', StringComparison.Ordinal) ? $"\"{value}\"" : value;
    }

    private static string GetMinecraftOsName()
    {
        if (OperatingSystem.IsWindows())
        {
            return "windows";
        }

        if (OperatingSystem.IsMacOS())
        {
            return "osx";
        }

        return "linux";
    }

    private static string RewriteUrl(string url, DownloadSource source)
    {
        return RewriteUrl(new Uri(url), source).ToString();
    }

    private static Uri RewriteUrl(Uri url, DownloadSource source)
    {
        if (source != DownloadSource.BMCLAPI)
        {
            return url;
        }

        var raw = url.ToString();
        raw = raw.Replace("https://launcher.mojang.com/", "https://bmclapi2.bangbang93.com/", StringComparison.OrdinalIgnoreCase);
        raw = raw.Replace("https://launchermeta.mojang.com/", "https://bmclapi2.bangbang93.com/", StringComparison.OrdinalIgnoreCase);
        raw = raw.Replace("https://piston-meta.mojang.com/", "https://bmclapi2.bangbang93.com/", StringComparison.OrdinalIgnoreCase);
        raw = raw.Replace("https://piston-data.mojang.com/", "https://bmclapi2.bangbang93.com/", StringComparison.OrdinalIgnoreCase);
        raw = raw.Replace("https://resources.download.minecraft.net/", "https://bmclapi2.bangbang93.com/assets/", StringComparison.OrdinalIgnoreCase);
        raw = raw.Replace("https://libraries.minecraft.net/", "https://bmclapi2.bangbang93.com/maven/", StringComparison.OrdinalIgnoreCase);
        return new Uri(raw);
    }

    private sealed class VersionManifest
    {
        public List<ManifestVersionEntry> Versions { get; init; } = [];
    }

    private sealed class ManifestVersionEntry
    {
        public string? Id { get; init; }

        public string? Type { get; init; }

        public Uri? Url { get; init; }

        public DateTimeOffset? ReleaseTime { get; init; }
    }

    private sealed class VersionMetadata
    {
        public string? Id { get; init; }

        public string? Type { get; init; }

        public string? MainClass { get; init; }

        public string? Assets { get; init; }

        public AssetIndexInfo? AssetIndex { get; init; }

        public VersionDownloads? Downloads { get; init; }

        public List<LibraryInfo>? Libraries { get; init; }

        public VersionArguments? Arguments { get; init; }

        public string? MinecraftArguments { get; init; }
    }

    private sealed class VersionDownloads
    {
        public LibraryDownloadInfo? Client { get; init; }
    }

    private sealed class AssetIndexInfo
    {
        public string? Id { get; init; }

        public string? Url { get; init; }
    }

    private sealed class AssetIndex
    {
        public Dictionary<string, AssetObjectInfo> Objects { get; init; } = [];
    }

    private sealed class AssetObjectInfo
    {
        public string Hash { get; init; } = string.Empty;
    }

    private sealed class LibraryInfo
    {
        public string? Name { get; init; }

        public LibraryDownloads? Downloads { get; init; }

        public List<RuleInfo>? Rules { get; init; }

        public Dictionary<string, string>? Natives { get; init; }

        public ExtractInfo? Extract { get; init; }
    }

    private sealed class LibraryDownloads
    {
        public LibraryDownloadInfo? Artifact { get; init; }

        public Dictionary<string, LibraryDownloadInfo>? Classifiers { get; init; }
    }

    private sealed class LibraryDownloadInfo
    {
        public string? Path { get; init; }

        public string? Url { get; init; }
    }

    private sealed class ExtractInfo
    {
        public List<string>? Exclude { get; init; }
    }

    private sealed class RuleInfo
    {
        public string? Action { get; init; }

        public RuleOsInfo? Os { get; init; }

        public Dictionary<string, JsonElement>? Features { get; init; }
    }

    private sealed class RuleOsInfo
    {
        public string? Name { get; init; }

        public string? Arch { get; init; }

        public string? Version { get; init; }
    }

    private sealed class VersionArguments
    {
        public List<JsonElement> Game { get; init; } = [];

        public List<JsonElement> Jvm { get; init; } = [];
    }
}
