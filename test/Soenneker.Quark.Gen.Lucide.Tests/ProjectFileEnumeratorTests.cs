using System;
using System.IO;
using System.Linq;
using System.Threading;
using Soenneker.Quark.Gen.Lucide.BuildTasks;

namespace Soenneker.Quark.Gen.Lucide.Tests;

public sealed class ProjectFileEnumeratorTests
{
    [Test]
    public void FiltersExtensionsAndPrunesExcludedDirectories()
    {
        string root = Path.Combine(Path.GetTempPath(), "quark-enumeration-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            foreach (string directory in new[] { "src", "src/nested", "bin", "obj", "node_modules", ".codex-work", ".git", "src/obj" })
            {
                string path = Path.Combine(root, directory);
                Directory.CreateDirectory(path);
                File.WriteAllText(Path.Combine(path, "View.razor"), "");
                File.WriteAllText(Path.Combine(path, "Code.cs"), "");
                File.WriteAllText(Path.Combine(path, "Ignored.txt"), "");
            }
            File.WriteAllText(Path.Combine(root, ".hidden.cs"), "");
            File.WriteAllText(Path.Combine(root, "Upper.CS"), "");

            string[] actual = ProjectFileEnumerator.EnumerateByExtensions(root, [".cs", ".razor"])
                .Select(path => Path.GetRelativePath(root, path).Replace('\\', '/')).OrderBy(path => path, StringComparer.Ordinal).ToArray();
            string[] expected = new[] { ".hidden.cs", "src/Code.cs", "src/View.razor", "src/nested/Code.cs", "src/nested/View.razor" };
            if (OperatingSystem.IsWindows())
                expected = expected.Append("Upper.CS").ToArray();
            if (!actual.SequenceEqual(expected.OrderBy(path => path, StringComparer.Ordinal)))
                throw new InvalidOperationException("Unexpected project files: " + string.Join(", ", actual));

            using var cancellation = new CancellationTokenSource();
            cancellation.Cancel();
            try
            {
                _ = ProjectFileEnumerator.EnumerateByExtension(root, ".cs", cancellation.Token).ToArray();
                throw new InvalidOperationException("Expected cancellation.");
            }
            catch (OperationCanceledException) { }
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Test]
    public void MetadataMatchesFileInfo()
    {
        string root = Path.Combine(Path.GetTempPath(), "quark-metadata-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            string path = Path.Combine(root, "input.cs");
            File.WriteAllText(path, "some source text");
            var entry = ProjectFileEnumerator.EnumerateMetadata(root, [".cs"]).Single();
            var info = new FileInfo(path);
            if (entry.Path != path || entry.Length != info.Length || entry.LastWriteTimeTicks != info.LastWriteTimeUtc.Ticks)
                throw new InvalidOperationException("Directory metadata differs from file metadata.");
        }
        finally { Directory.Delete(root, recursive: true); }
    }

    [Test]
    public void MissingRootIsEmpty()
    {
        if (ProjectFileEnumerator.EnumerateByExtension(Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N")), ".cs").Any())
            throw new InvalidOperationException("A missing root should not return files.");
    }
}
