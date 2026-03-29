using System.Collections.Generic;

namespace AZLuncher.Models;

public enum VersionIsolationMaterializationMode
{
    SymbolicLink,
    Copy,
}

public sealed class VersionIsolationDescriptor
{
    public required string ResourceId { get; init; }

    public required string SourceFilePath { get; init; }

    public required string TargetSubdirectory { get; init; }

    public required string TargetFileName { get; init; }
}

public sealed class VersionIsolationPreparedResource
{
    public required string ResourceId { get; init; }

    public required string StorePath { get; init; }

    public required string TargetPath { get; init; }

    public VersionIsolationMaterializationMode MaterializationMode { get; init; }
}

public sealed class VersionIsolationResult
{
    public required string InstanceId { get; init; }

    public required string InstanceRoot { get; init; }

    public required string StoreRoot { get; init; }

    public required string ManifestPath { get; init; }

    public required IReadOnlyList<VersionIsolationPreparedResource> Resources { get; init; }
}
