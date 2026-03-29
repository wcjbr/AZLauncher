using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using AZLuncher.Models;

namespace AZLuncher.Services;

public sealed class VersionIsolationService
{
    private static readonly JsonSerializerOptions ManifestJsonOptions = new()
    {
        WriteIndented = true,
    };

    private readonly string isolationRoot;
    private readonly string storeRoot;
    private readonly string manifestRoot;

    public VersionIsolationService()
    {
        isolationRoot = Path.Combine(AppContext.BaseDirectory, "AZL", "isolation-v3");
        storeRoot = Path.Combine(isolationRoot, "store");
        manifestRoot = Path.Combine(isolationRoot, "manifests");
    }

    public string IsolationRoot => isolationRoot;

    public string StoreRoot => storeRoot;

    public async Task<VersionIsolationResult> PrepareInstanceAsync(
        string instanceId,
        string instanceRoot,
        IEnumerable<VersionIsolationDescriptor> resources,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(instanceId))
        {
            throw new ArgumentException("Instance ID is required.", nameof(instanceId));
        }

        if (string.IsNullOrWhiteSpace(instanceRoot))
        {
            throw new ArgumentException("Instance root is required.", nameof(instanceRoot));
        }

        Directory.CreateDirectory(storeRoot);
        Directory.CreateDirectory(manifestRoot);
        Directory.CreateDirectory(instanceRoot);

        var preparedResources = new List<VersionIsolationPreparedResource>();

        foreach (var resource in resources)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var storePath = EnsureStoreEntry(resource);
            var targetSubdirectory = NormalizeRelativeDirectory(resource.TargetSubdirectory);
            var targetDirectory = string.IsNullOrWhiteSpace(targetSubdirectory)
                ? instanceRoot
                : Path.Combine(instanceRoot, targetSubdirectory);

            Directory.CreateDirectory(targetDirectory);

            var targetPath = Path.Combine(targetDirectory, resource.TargetFileName);
            var materializationMode = MaterializeLaunchFile(storePath, targetPath);

            preparedResources.Add(new VersionIsolationPreparedResource
            {
                ResourceId = resource.ResourceId,
                StorePath = storePath,
                TargetPath = targetPath,
                MaterializationMode = materializationMode,
            });
        }

        var manifestPath = Path.Combine(manifestRoot, $"{SanitizeFileName(instanceId)}.json");
        var manifest = new VersionIsolationResult
        {
            InstanceId = instanceId,
            InstanceRoot = instanceRoot,
            StoreRoot = storeRoot,
            ManifestPath = manifestPath,
            Resources = preparedResources,
        };

        await using var manifestStream = File.Create(manifestPath);
        await JsonSerializer.SerializeAsync(manifestStream, manifest, ManifestJsonOptions, cancellationToken);

        return manifest;
    }

    private string EnsureStoreEntry(VersionIsolationDescriptor resource)
    {
        if (string.IsNullOrWhiteSpace(resource.ResourceId))
        {
            throw new ArgumentException("Resource ID is required.", nameof(resource));
        }

        if (string.IsNullOrWhiteSpace(resource.SourceFilePath))
        {
            throw new ArgumentException("Source file path is required.", nameof(resource));
        }

        if (string.IsNullOrWhiteSpace(resource.TargetFileName))
        {
            throw new ArgumentException("Target file name is required.", nameof(resource));
        }

        var sourcePath = Path.GetFullPath(resource.SourceFilePath);
        if (!File.Exists(sourcePath))
        {
            throw new FileNotFoundException("Source resource file was not found.", sourcePath);
        }

        var storePath = Path.Combine(storeRoot, resource.ResourceId);
        if (!File.Exists(storePath))
        {
            File.Copy(sourcePath, storePath, overwrite: false);
        }

        return storePath;
    }

    private static VersionIsolationMaterializationMode MaterializeLaunchFile(string storePath, string targetPath)
    {
        if (Path.Exists(targetPath))
        {
            File.Delete(targetPath);
        }

        try
        {
            File.CreateSymbolicLink(targetPath, storePath);
            return VersionIsolationMaterializationMode.SymbolicLink;
        }
        catch (Exception ex) when (ex is PlatformNotSupportedException
                                   or UnauthorizedAccessException
                                   or IOException
                                   or NotSupportedException)
        {
            File.Copy(storePath, targetPath, overwrite: true);
            return VersionIsolationMaterializationMode.Copy;
        }
    }

    private static string NormalizeRelativeDirectory(string relativeDirectory)
    {
        if (string.IsNullOrWhiteSpace(relativeDirectory))
        {
            return string.Empty;
        }

        var normalized = relativeDirectory
            .Replace('\\', Path.DirectorySeparatorChar)
            .Replace('/', Path.DirectorySeparatorChar)
            .Trim(Path.DirectorySeparatorChar);

        if (Path.IsPathRooted(normalized))
        {
            throw new InvalidOperationException("Target subdirectory must be relative.");
        }

        var segments = normalized.Split(Path.DirectorySeparatorChar, StringSplitOptions.RemoveEmptyEntries);
        if (segments.Any(segment => segment == ".."))
        {
            throw new InvalidOperationException("Target subdirectory cannot contain parent traversal.");
        }

        return normalized;
    }

    private static string SanitizeFileName(string value)
    {
        var sanitized = value;
        foreach (var invalidChar in Path.GetInvalidFileNameChars())
        {
            sanitized = sanitized.Replace(invalidChar, '_');
        }

        return sanitized;
    }
}
