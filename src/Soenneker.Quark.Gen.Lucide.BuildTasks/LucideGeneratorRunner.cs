using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
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

/// <inheritdoc cref="ILucideGeneratorRunner"/>
public sealed class LucideGeneratorRunner : ILucideGeneratorRunner
{
    private static readonly Regex _csIconPattern = new(
        @"LucideIcon\.([A-Za-z0-9_]+)",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

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

        if (!map.TryGetValue("--projectDir", out string? projectDir) || string.IsNullOrWhiteSpace(projectDir))
        {
            return Fail("Missing required --projectDir");
        }

        projectDir = Path.GetFullPath(projectDir.Trim().Trim('"'));

        if (!await _directoryUtil.Exists(projectDir, cancellationToken).NoSync())
        {
            return Fail($"Project directory does not exist: {projectDir}");
        }

        string outputPath = map.TryGetValue("--output", out string? outVal) && !string.IsNullOrWhiteSpace(outVal)
            ? Path.GetFullPath(outVal.Trim().Trim('"'))
            : Path.Combine(projectDir, "obj", "Generated", "LucideIconSvgMap.g.cs");

        HashSet<string> icons = await CollectIconsFromProject(projectDir, cancellationToken).NoSync();
        if (icons.Count == 0)
        {
            _logger.LogInformation("No LucideIcon usages found. Skipping LucideIconSvgMap generation.");
            return 0;
        }

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

        if (!await _directoryUtil.Exists(resourcesDir, cancellationToken).NoSync())
        {
            _logger.LogWarning("Lucide Resources directory does not exist: {Path}. LucideIconSvgMap will have no SVG content.", resourcesDir);
        }

        string content = await GenerateLucideIconSvgMap(icons, resourcesDir, cancellationToken).NoSync();
        string? outputDir = Path.GetDirectoryName(outputPath);

        if (outputDir.HasContent())
        {
            await _directoryUtil.CreateIfDoesNotExist(outputDir, true, cancellationToken).NoSync();
        }

        await _fileUtil.Write(outputPath, content, log: true, cancellationToken).NoSync();
        _logger.LogInformation("Generated {Output} with {Count} icons.", outputPath, icons.Count);

        string providerPath = Path.Combine(outputDir ?? _directoryUtil.GetWorkingDirectory(), "LucideIconSvgProvider.g.cs");
        string providerContent = GenerateLucideIconSvgProvider();
        await _fileUtil.Write(providerPath, providerContent, log: true, cancellationToken).NoSync();
        _logger.LogInformation("Generated {ProviderPath}.", providerPath);

        string extensionsPath = Path.Combine(outputDir ?? _directoryUtil.GetWorkingDirectory(), "LucideIconServiceCollectionExtensions.g.cs");
        string extensionsContent = GenerateLucideIconServiceCollectionExtensions();
        await _fileUtil.Write(extensionsPath, extensionsContent, log: true, cancellationToken).NoSync();
        _logger.LogInformation("Generated {ExtensionsPath}.", extensionsPath);

        return 0;
    }

    private async Task<HashSet<string>> CollectIconsFromProject(string projectDir, CancellationToken ct)
    {
        var icons = new HashSet<string>(StringComparer.Ordinal);
        List<string> csFiles = await _directoryUtil.GetFilesByExtension(projectDir, ".cs", recursive: true, ct).NoSync();
        List<string> razorFiles = await _directoryUtil.GetFilesByExtension(projectDir, ".razor", recursive: true, ct).NoSync();
        IEnumerable<string> allFiles = csFiles.Concat(razorFiles)
            .Where(p => !p.Contains("\\obj\\", StringComparison.Ordinal) && !p.Contains("/obj/", StringComparison.Ordinal)
                && !p.Contains("\\bin\\", StringComparison.Ordinal) && !p.Contains("/bin/", StringComparison.Ordinal));

        foreach (string file in allFiles)
        {
            ct.ThrowIfCancellationRequested();
            string? content = await _fileUtil.TryRead(file, log: false, ct).NoSync();
            if (string.IsNullOrEmpty(content))
            {
                continue;
            }

            // Use _csIconPattern for both: it matches any LucideIcon.Name (method args, Icon="...", etc.)
            foreach (Match m in _csIconPattern.Matches(content))
            {
                if (m.Success && m.Groups.Count >= 2)
                {
                    string name = m.Groups[1].Value;
                    if (!string.IsNullOrEmpty(name))
                    {
                        icons.Add(name);
                    }
                }
            }
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
        sb.AppendLine("public static partial class LucideIconSvgMap");
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
        sb.AppendLine("public sealed class LucideIconSvgProvider : ILucideIconSvgProvider");
        sb.AppendLine("{");
        sb.AppendLine("    /// <inheritdoc />");
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
        sb.AppendLine("        return services.AddScoped<ILucideIconSvgProvider, LucideIconSvgProvider>();");
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
        var line = $"Soenneker.Quark.Gen.Lucide.BuildTasks: {message}";
        Console.Error.WriteLine(line);
        return 1;
    }
}
