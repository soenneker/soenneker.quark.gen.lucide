using System;
using System.Collections.Generic;
using System.IO;
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

    public static IEnumerable<string> EnumerateByExtension(string rootDirectory, string extension, CancellationToken cancellationToken = default)
    {
        if (!Directory.Exists(rootDirectory))
            yield break;

        var pendingDirectories = new Stack<string>();
        pendingDirectories.Push(rootDirectory);

        while (pendingDirectories.Count > 0)
        {
            cancellationToken.ThrowIfCancellationRequested();
            string directory = pendingDirectories.Pop();
            string[] files;
            string[] childDirectories;

            try
            {
                files = Directory.GetFiles(directory, "*" + extension, SearchOption.TopDirectoryOnly);
                childDirectories = Directory.GetDirectories(directory, "*", SearchOption.TopDirectoryOnly);
            }
            catch (Exception exception) when (exception is UnauthorizedAccessException or DirectoryNotFoundException or IOException)
            {
                continue;
            }

            foreach (string file in files)
                yield return file;

            foreach (string childDirectory in childDirectories)
            {
                string name = Path.GetFileName(childDirectory);

                if (_excludedDirectoryNames.Contains(name) || name.StartsWith(".codex", StringComparison.OrdinalIgnoreCase) || IsReparsePoint(childDirectory))
                    continue;

                pendingDirectories.Push(childDirectory);
            }
        }
    }

    private static bool IsReparsePoint(string directory)
    {
        try
        {
            return (File.GetAttributes(directory) & FileAttributes.ReparsePoint) != 0;
        }
        catch (Exception exception) when (exception is UnauthorizedAccessException or DirectoryNotFoundException or IOException)
        {
            return true;
        }
    }
}
