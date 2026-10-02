using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Soenneker.Extensions.String;
using Soenneker.Extensions.Task;
using Soenneker.Extensions.ValueTask;
using Soenneker.Quark.Gen.Lucide.BuildTasks.Abstract;
using Soenneker.Utils.Case;
using Soenneker.Utils.Directory.Abstract;
using Soenneker.Utils.File.Abstract;
using Soenneker.Utils.PooledStringBuilders;

namespace Soenneker.Quark.Gen.Lucide.BuildTasks;

/// <inheritdoc cref="ILucideGeneratorRunner" />
public sealed class LucideGeneratorRunner : ILucideGeneratorRunner
{
    private readonly ILogger<LucideGeneratorRunner> _logger;
    private readonly IFileUtil _fileUtil;
    private readonly IDirectoryUtil _directoryUtil;

    public LucideGeneratorRunner(ILogger<LucideGeneratorRunner> logger, IFileUtil fileUtil, IDirectoryUtil directoryUtil)
    {
        _logger = logger;
        _fileUtil = fileUtil;
        _directoryUtil = directoryUtil;
    }

    public async ValueTask<int> Run(CancellationToken cancellationToken = default)
    {
        string[] args = Environment.GetCommandLineArgs();
        Dictionary<string, string> map = ParseArgs(args);

        if (!map.TryGetValue("--projectDir", out string? projectDir) || projectDir.IsNullOrWhiteSpace())
        {
            return Fail("Missing required --projectDir");
        }

        projectDir = Path.GetFullPath(projectDir.Trim().Trim('"'));

        if (!await _directoryUtil.Exists(projectDir, cancellationToken).NoSync())
        {
            return Fail($"Project directory does not exist: {projectDir}");
        }

        string outputPath = map.TryGetValue("--output", out string? outVal) && outVal.HasContent()
            ? Path.GetFullPath(outVal.Trim().Trim('"'))
            : Path.Combine(projectDir, "obj", "Generated", "LucideIconSvgMap.g.cs");

        _logger.LogInformation("Starting Lucide icon generation for project {ProjectDir}.", projectDir);

        // SVGs live in build directory / Resources (e.g. $(OutputPath)Resources). Use explicit path if provided, else projectDir/Resources.
        string resourcesDir;
        if (map.TryGetValue("--resourcesPath", out string? resPath) && !string.IsNullOrWhiteSpace(resPath))
        {
            resourcesDir = Path.GetFullPath(resPath.Trim().Trim('"'));
        }
        else
        {
            resourcesDir = Path.Combine(projectDir, "Resources");
        }

        string? outputDir = Path.GetDirectoryName(outputPath);
        string outputRoot = outputDir.HasContent() ? outputDir! : _directoryUtil.GetWorkingDirectory();
        string providerPath = Path.Combine(outputRoot, "LucideIconSvgProvider.g.cs");
        string extensionsPath = Path.Combine(outputRoot, "LucideIconServiceCollectionExtensions.g.cs");
        string hashPath = Path.Combine(outputRoot, "lucide-generator.inputs.hash");

        string inputHash = await ComputeInputHash(projectDir, resourcesDir, cancellationToken).NoSync();

        if (await CanSkipGeneration(inputHash, hashPath, outputPath, providerPath, extensionsPath, cancellationToken).NoSync())
        {
            _logger.LogInformation("Lucide inputs unchanged. Skipping generation for project {ProjectDir}.", projectDir);
            return 0;
        }

        _logger.LogInformation("Collecting Lucide icon usages from project sources...");
        HashSet<string> icons = await CollectIconsFromProject(projectDir, cancellationToken).NoSync();

        if (icons.Count == 0)
        {
            _logger.LogInformation("No LucideIcon usages found. Generating empty Lucide outputs so DI registration remains available.");
        }

        _logger.LogInformation("Generating Lucide outputs using resources at {ResourcesDir}.", resourcesDir);

        if (icons.Count > 0 && !await HasSvgResources(resourcesDir, cancellationToken).NoSync())
        {
            return Fail($"Lucide resources directory contains no SVG files: {resourcesDir}. Check the Soenneker.Lucide.Icons package contentFiles path.");
        }

        _logger.LogInformation("Generating LucideIconSvgMap for {Count} icons.", icons.Count);
        string content = await GenerateLucideIconSvgMap(icons, resourcesDir, cancellationToken).NoSync();

        if (outputDir.HasContent())
        {
            _logger.LogInformation("Ensuring output directory exists at {OutputDir}.", outputDir);
            await _directoryUtil.Create(outputDir, true, cancellationToken).NoSync();
        }

        _logger.LogInformation("Writing LucideIconSvgMap to {OutputPath}.", outputPath);
        await _fileUtil.Write(outputPath, content, log: true, cancellationToken).NoSync();
        _logger.LogInformation("Generated {Output} with {Count} icons.", outputPath, icons.Count);

        string providerContent = GenerateLucideIconSvgProvider();
        _logger.LogInformation("Writing LucideIconSvgProvider to {ProviderPath}.", providerPath);
        await _fileUtil.Write(providerPath, providerContent, log: true, cancellationToken).NoSync();
        _logger.LogInformation("Generated {ProviderPath}.", providerPath);

        string extensionsContent = GenerateLucideIconServiceCollectionExtensions();
        _logger.LogInformation("Writing LucideIconServiceCollectionExtensions to {ExtensionsPath}.", extensionsPath);
        await _fileUtil.Write(extensionsPath, extensionsContent, log: true, cancellationToken).NoSync();
        _logger.LogInformation("Generated {ExtensionsPath}.", extensionsPath);
        await _fileUtil.Write(hashPath, inputHash, log: false, cancellationToken).NoSync();
        _logger.LogInformation("Completed Lucide icon generation for project {ProjectDir}.", projectDir);

        return 0;
    }

    private async ValueTask<bool> CanSkipGeneration(string inputHash, string hashPath, string outputPath, string providerPath, string extensionsPath,
        CancellationToken cancellationToken)
    {
        if (!await _fileUtil.Exists(outputPath, cancellationToken).NoSync() ||
            !await _fileUtil.Exists(providerPath, cancellationToken).NoSync() ||
            !await _fileUtil.Exists(extensionsPath, cancellationToken).NoSync() ||
            !await _fileUtil.Exists(hashPath, cancellationToken).NoSync())
        {
            return false;
        }

        string? previousHash = await _fileUtil.TryRead(hashPath, log: false, cancellationToken).NoSync();
        return string.Equals(previousHash?.Trim(), inputHash, StringComparison.Ordinal);
    }

    private async ValueTask<string> ComputeInputHash(string projectDir, string resourcesDir, CancellationToken cancellationToken)
    {
        var entries = new List<string>();

        AddFileMetadataEntries(entries, projectDir, [".cs", ".razor"], cancellationToken);
        AddFileMetadataEntries(entries, resourcesDir, [".svg"], cancellationToken);

        string assemblyLocation = System.IO.Path.Combine(AppContext.BaseDirectory, typeof(LucideGeneratorRunner).Assembly.GetName().Name + ".dll");
        if (!System.IO.File.Exists(assemblyLocation))
            assemblyLocation = Environment.ProcessPath ?? string.Empty;
        if (!string.IsNullOrWhiteSpace(assemblyLocation) && (await _fileUtil.Exists(assemblyLocation).NoSync()))
        {
            entries.Add(BuildMetadataEntry("buildtasks", assemblyLocation, assemblyLocation));
        }

        entries.Sort(StringComparer.Ordinal);

        return MetadataHash.Compute(entries);
    }

    private static void AddFileMetadataEntries(List<string> entries, string rootDir, string[] extensions, CancellationToken cancellationToken)
    {
        foreach ((string file, long length, long lastWriteTimeTicks) in ProjectFileEnumerator.EnumerateMetadata(rootDir, extensions, cancellationToken))
        {
            if (IsExcludedProjectPath(file))
                continue;

            ReadOnlySpan<char> actualExtension = Path.GetExtension(file.AsSpan());
            foreach (string extension in extensions)
            {
                if (!actualExtension.Equals(extension, StringComparison.OrdinalIgnoreCase))
                    continue;

                string relativePath = Path.GetRelativePath(rootDir, file).Replace('\\', '/');
                entries.Add($"{extension}|{relativePath}|{length}|{lastWriteTimeTicks}");
                break;
            }
        }
    }

    private async ValueTask<bool> HasSvgResources(string resourcesDir, CancellationToken cancellationToken)
    {
        if (!await _directoryUtil.Exists(resourcesDir, cancellationToken).NoSync())
            return false;

        cancellationToken.ThrowIfCancellationRequested();
        return Directory.EnumerateFiles(resourcesDir, "*.svg", SearchOption.TopDirectoryOnly).Any();
    }

    private static string BuildMetadataEntry(string rootDir, string filePath, string category)
    {
        var info = new FileInfo(filePath);
        string relativePath = Path.GetRelativePath(rootDir, filePath).Replace('\\', '/');
        return $"{category}|{relativePath}|{info.Length}|{info.LastWriteTimeUtc.Ticks}";
    }

    private static bool IsExcludedProjectPath(string path)
    {
        return path.Contains("\\obj\\", StringComparison.OrdinalIgnoreCase) ||
               path.Contains("/obj/", StringComparison.OrdinalIgnoreCase) ||
               path.Contains("\\bin\\", StringComparison.OrdinalIgnoreCase) ||
               path.Contains("/bin/", StringComparison.OrdinalIgnoreCase);
    }

    private async Task<HashSet<string>> CollectIconsFromProject(string projectDir, CancellationToken ct)
    {
        var icons = new HashSet<string>(StringComparer.Ordinal);
        IEnumerable<string> allFiles = ProjectFileEnumerator.EnumerateByExtensions(projectDir, [".cs", ".razor"], ct);

        foreach (string file in allFiles)
        {
            ct.ThrowIfCancellationRequested();
            string? content = await _fileUtil.TryRead(file, log: false, ct).NoSync();
            if (string.IsNullOrEmpty(content))
            {
                continue;
            }

            IconUsageScanner.Collect(content, "LucideIcon.", icons);
        }

        return icons;
    }

    /// <param name="resourcesDir">Directory containing the SVG files (e.g. project Resources or $(OutputPath)Resources).</param>
    private async ValueTask<string?> ReadSvgContent(string resourcesDir, string kebabIconName, CancellationToken cancellationToken)
    {
        string path = Path.Combine(resourcesDir, kebabIconName + ".svg");
        return await _fileUtil.TryRead(path, log: false, cancellationToken).NoSync();
    }

    private static string EscapeForCSharpString(string value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return value;
        }

        return value
            .Replace("\\", "\\\\")
            .Replace("\"", "\\\"")
            .Replace("\r\n", "\\n")
            .Replace("\n", "\\n")
            .Replace("\r", "\\n");
    }

    private async Task<string> GenerateLucideIconSvgMap(HashSet<string> iconNames, string? resourcesDir, CancellationToken cancellationToken)
    {
        var sb = new StringBuilder();
        sb.AppendLine("// <auto-generated/>");
        sb.AppendLine("#nullable enable");
        sb.AppendLine();
        sb.AppendLine("namespace Soenneker.Quark.Gen.Lucide.Generated;");
        sb.AppendLine();
        sb.AppendLine("/// <summary>");
        sb.AppendLine("/// Maps Lucide icon names (PascalCase) to SVG content from Soenneker.Lucide.Icons.");
        sb.AppendLine("/// </summary>");
        sb.AppendLine("internal static partial class LucideIconSvgMap");
        sb.AppendLine("{");
        sb.AppendLine("    /// <summary>");
        sb.AppendLine("    /// Returns the SVG markup for the given Lucide icon name, or null if not found.");
        sb.AppendLine("    /// </summary>");
        sb.AppendLine("    public static string? GetSvg(string iconName)");
        sb.AppendLine("    {");
        sb.AppendLine("        return iconName switch");
        sb.AppendLine("        {");

        foreach (string iconName in iconNames.OrderBy(x => x, StringComparer.Ordinal))
        {
            cancellationToken.ThrowIfCancellationRequested();
            string kebab = CaseUtil.ToKebab(iconName);
            string? svgContent = resourcesDir != null ? await ReadSvgContent(resourcesDir, kebab, cancellationToken).NoSync() : null;
            if (svgContent != null)
            {
                string escaped = EscapeForCSharpString(svgContent);
                sb.Append("            \"").Append(iconName).Append("\" => \"").Append(escaped).AppendLine("\",");
            }
        }

        sb.AppendLine("            _ => null");
        sb.AppendLine("        };");
        sb.AppendLine("    }");
        sb.AppendLine("}");
        return sb.ToString();
    }

    private static string GenerateLucideIconSvgProvider()
    {
        using var sb = new PooledStringBuilder();
        sb.AppendLine("// <auto-generated/>");
        sb.AppendLine("#nullable enable");
        sb.AppendLine();
        sb.AppendLine("using Soenneker.Quark.Gen.Lucide.Abstractions;");
        sb.AppendLine();
        sb.AppendLine("namespace Soenneker.Quark.Gen.Lucide.Generated;");
        sb.AppendLine();
        sb.AppendLine("/// <summary>");
        sb.AppendLine("/// Implements <see cref=\"ILucideIconSvgProvider\"/> using the generated <see cref=\"LucideIconSvgMap\"/>.");
        sb.AppendLine("/// </summary>");
        sb.AppendLine("internal sealed class LucideIconSvgProvider : ILucideIconSvgProvider");
        sb.AppendLine("{");
        sb.AppendLine("    public string? GetSvg(string iconName) => LucideIconSvgMap.GetSvg(iconName);");
        sb.AppendLine("}");
        return sb.ToString();
    }

    private static string GenerateLucideIconServiceCollectionExtensions()
    {
        using var sb = new PooledStringBuilder();
        sb.AppendLine("// <auto-generated/>");
        sb.AppendLine("#nullable enable");
        sb.AppendLine();
        sb.AppendLine("using Microsoft.Extensions.DependencyInjection;");
        sb.AppendLine("using Microsoft.Extensions.DependencyInjection.Extensions;");
        sb.AppendLine("using Soenneker.Quark.Gen.Lucide.Abstractions;");
        sb.AppendLine();
        sb.AppendLine("namespace Soenneker.Quark.Gen.Lucide.Generated;");
        sb.AppendLine();
        sb.AppendLine("/// <summary>");
        sb.AppendLine("/// Extension methods for registering the generated Lucide icon SVG provider.");
        sb.AppendLine("/// </summary>");
        sb.AppendLine("public static class LucideIconServiceCollectionExtensions");
        sb.AppendLine("{");
        sb.AppendLine("    /// <summary>");
        sb.AppendLine("    /// Registers <see cref=\"ILucideIconSvgProvider\"/> and <see cref=\"LucideIconSvgProvider\"/> as scoped.");
        sb.AppendLine("    /// </summary>");
        sb.AppendLine("    public static IServiceCollection AddLucideIconsAsScoped(this IServiceCollection services)");
        sb.AppendLine("    {");
        sb.AppendLine("        services.TryAddScoped<ILucideIconSvgProvider, LucideIconSvgProvider>();");
        sb.AppendLine("        return services;");
        sb.AppendLine("    }");
        sb.AppendLine("}");
        return sb.ToString();
    }

    private static Dictionary<string, string> ParseArgs(string[] args)
    {
        var map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        for (var i = 0; i < args.Length; i++)
        {
            if (args[i].StartsWith("--", StringComparison.Ordinal) && i + 1 < args.Length)
            {
                map[args[i]] = args[i + 1];
                i++;
            }
        }
        return map;
    }

    private static int Fail(string message)
    {
        Console.Error.WriteLine($"Soenneker.Quark.Gen.Lucide.BuildTasks: {message}");
        return 1;
    }
}
