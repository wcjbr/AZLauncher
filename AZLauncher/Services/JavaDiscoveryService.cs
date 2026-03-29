using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AZLauncher.Models;

namespace AZLauncher.Services;

public sealed class JavaDiscoveryService
{
    public async Task<IReadOnlyList<JavaRuntimeCandidate>> DiscoverAsync(CancellationToken cancellationToken = default)
    {
        var candidates = new Dictionary<string, JavaRuntimeCandidate>(StringComparer.OrdinalIgnoreCase);

        foreach (var path in GetCandidateJavaPaths())
        {
            cancellationToken.ThrowIfCancellationRequested();
            var candidate = await TryBuildCandidateAsync(path, cancellationToken);
            if (candidate is null)
            {
                continue;
            }

            candidates[path] = candidate;
        }

        return candidates.Values
            .OrderByDescending(candidate => candidate.VersionLabel, StringComparer.OrdinalIgnoreCase)
            .ThenBy(candidate => candidate.DisplayName, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private static IEnumerable<string> GetCandidateJavaPaths()
    {
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var results = new List<string>();

        void Add(string? rawPath)
        {
            if (string.IsNullOrWhiteSpace(rawPath))
            {
                return;
            }

            try
            {
                var fullPath = Path.GetFullPath(rawPath);
                if (File.Exists(fullPath) && seen.Add(fullPath))
                {
                    results.Add(fullPath);
                }
            }
            catch
            {
            }
        }

        var javaHome = Environment.GetEnvironmentVariable("JAVA_HOME");
        Add(string.IsNullOrWhiteSpace(javaHome) ? null : Path.Combine(javaHome, "bin", GetJavaFileName()));

        var pathEnv = Environment.GetEnvironmentVariable("PATH") ?? string.Empty;
        foreach (var segment in pathEnv.Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries))
        {
            Add(Path.Combine(segment.Trim(), GetJavaFileName()));
        }

        foreach (var path in EnumerateCommonJavaLocations())
        {
            Add(path);
        }

        return results;
    }

    private static IEnumerable<string> EnumerateCommonJavaLocations()
    {
        var roots = new List<string>();

        if (OperatingSystem.IsWindows())
        {
            roots.AddRange(
            [
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "Java"),
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "Eclipse Adoptium"),
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), "Java"),
            ]);
        }
        else if (OperatingSystem.IsMacOS())
        {
            roots.Add("/Library/Java/JavaVirtualMachines");
            roots.Add("/opt/homebrew/opt");
            roots.Add("/usr/local/opt");
        }
        else
        {
            roots.AddRange(
            [
                "/usr/bin",
                "/usr/local/bin",
                "/usr/lib/jvm",
                "/usr/lib64/jvm",
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".sdkman", "candidates", "java"),
            ]);
        }

        foreach (var root in roots.Where(Directory.Exists))
        {
            foreach (var executable in Directory.EnumerateFiles(root, GetJavaFileName(), SearchOption.AllDirectories))
            {
                yield return executable;
            }
        }
    }

    private static async Task<JavaRuntimeCandidate?> TryBuildCandidateAsync(string executablePath, CancellationToken cancellationToken)
    {
        try
        {
            var startInfo = new ProcessStartInfo
            {
                FileName = executablePath,
                UseShellExecute = false,
                RedirectStandardError = true,
                RedirectStandardOutput = true,
            };
            startInfo.ArgumentList.Add("-version");

            using var process = Process.Start(startInfo);
            if (process is null)
            {
                return null;
            }

            var stdoutTask = process.StandardOutput.ReadToEndAsync(cancellationToken);
            var stderrTask = process.StandardError.ReadToEndAsync(cancellationToken);
            await process.WaitForExitAsync(cancellationToken);

            var output = $"{await stdoutTask}\n{await stderrTask}";
            if (process.ExitCode != 0)
            {
                return null;
            }

            var firstLine = output
                .Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries)
                .FirstOrDefault()
                ?.Trim()
                ?? "Java";

            var versionLabel = firstLine.Replace("\"", string.Empty, StringComparison.Ordinal);
            var parentDirectory = Directory.GetParent(executablePath)?.Parent?.Name ?? "Java";

            return new JavaRuntimeCandidate
            {
                DisplayName = $"{parentDirectory} · {versionLabel}",
                ExecutablePath = executablePath,
                VersionLabel = versionLabel,
                SourceLabel = Directory.GetParent(executablePath)?.Parent?.FullName ?? string.Empty,
            };
        }
        catch
        {
            return null;
        }
    }

    private static string GetJavaFileName()
    {
        return OperatingSystem.IsWindows() ? "java.exe" : "java";
    }
}
