using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Enumeration;
using System.Threading;

namespace Soenneker.Quark.Gen.Lucide.BuildTasks;

internal static class ProjectFileEnumerator
{
    private static readonly HashSet<string> _excludedDirectoryNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "bin", "obj", "artifacts", "node_modules", "packages", "TestResults", "coverage", "dist", "out", "output",
        ".git", ".hg", ".svn", ".vs", ".vscode", ".idea", ".cache", ".nuget", ".playwright", ".playwright-cli",
        "BenchmarkDotNet.Artifacts"
    };

    private static readonly EnumerationOptions _options = new()
    {
        AttributesToSkip = 0,
        IgnoreInaccessible = true
    };

    public static IEnumerable<string> EnumerateByExtension(string rootDirectory, string extension, CancellationToken cancellationToken = default) =>
        EnumerateByExtensions(rootDirectory, [extension], cancellationToken);

    public static IEnumerable<string> EnumerateByExtensions(string rootDirectory, string[] extensions, CancellationToken cancellationToken = default) =>
        Enumerate(rootDirectory, extensions, static (ref FileSystemEntry entry) => entry.ToFullPath(), cancellationToken);

    public static IEnumerable<(string Path, long Length, long LastWriteTimeTicks)> EnumerateMetadata(string rootDirectory, string[] extensions,
        CancellationToken cancellationToken = default) =>
        Enumerate(rootDirectory, extensions,
            static (ref FileSystemEntry entry) => (entry.ToFullPath(), entry.Length, entry.LastWriteTimeUtc.UtcTicks), cancellationToken);

    private static IEnumerable<T> Enumerate<T>(string rootDirectory, string[] extensions, FileSystemEnumerable<T>.FindTransform transform,
        CancellationToken cancellationToken)
    {
        if (!Directory.Exists(rootDirectory))
            yield break;

        var pendingDirectories = new Stack<string>();
        pendingDirectories.Push(rootDirectory);
        StringComparison comparison = OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;

        // Filter before creating full paths, and reuse attributes supplied by the directory scan.
        bool Include(ref FileSystemEntry entry)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (entry.IsDirectory)
            {
                if ((entry.Attributes & FileAttributes.ReparsePoint) == 0 &&
                    !_excludedDirectoryNames.GetAlternateLookup<ReadOnlySpan<char>>().Contains(entry.FileName) &&
                    !entry.FileName.StartsWith(".codex", StringComparison.OrdinalIgnoreCase))
                    pendingDirectories.Push(entry.ToFullPath());

                return false;
            }

            foreach (string extension in extensions)
            {
                if (entry.FileName.EndsWith(extension, comparison))
                    return true;
            }

            return false;
        }

        while (pendingDirectories.Count > 0)
        {
            cancellationToken.ThrowIfCancellationRequested();
            IEnumerator<T> enumerator;
            try
            {
                var entries = new FileSystemEnumerable<T>(pendingDirectories.Pop(), transform, _options)
                {
                    ShouldIncludePredicate = Include
                };
                enumerator = entries.GetEnumerator();
            }
            catch (Exception exception) when (exception is UnauthorizedAccessException or IOException)
            {
                continue;
            }

            using (enumerator)
            {
                while (TryMoveNext(enumerator))
                    yield return enumerator.Current;
            }
        }
    }

    private static bool TryMoveNext<T>(IEnumerator<T> enumerator)
    {
        try
        {
            return enumerator.MoveNext();
        }
        catch (Exception exception) when (exception is UnauthorizedAccessException or IOException)
        {
            return false;
        }
    }
}
