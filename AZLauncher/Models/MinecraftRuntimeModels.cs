namespace AZLauncher.Models;

public sealed class DownloadableGameVersion
{
    public required string Id { get; init; }

    public required string Type { get; init; }

    public required string ReleaseTime { get; init; }

    public bool IsInstalled { get; init; }

    public override string ToString()
    {
        return $"{Id} ({Type})";
    }
}

public sealed class RuntimeProgressUpdate
{
    public required double Progress { get; init; }

    public required string Message { get; init; }
}

public sealed class LaunchExecutionResult
{
    public required bool Started { get; init; }

    public int? ProcessId { get; init; }

    public required string CommandPreview { get; init; }

    public required string Message { get; init; }
}
