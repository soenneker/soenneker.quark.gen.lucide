using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Soenneker.Quark.Gen.Lucide.BuildTasks.Abstract;

namespace Soenneker.Quark.Gen.Lucide.BuildTasks;

/// <inheritdoc cref="ILucideGeneratorRunner"/>
public sealed class LucideGeneratorRunner : ILucideGeneratorRunner
{
    private static readonly Regex _csIconPattern = new(
        @"LucideIcon\.([A-Za-z0-9_]+)",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private const string _packageId = "soenneker.lucide.icons";
    private const string _resourcesSubPath = "contentFiles/any/any/Resources";

    private readonly ILogger<LucideGeneratorRunner> _logger;

    public LucideGeneratorRunner(ILogger<LucideGeneratorRunner> logger)
    {
        _logger = logger;
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

        if (!Directory.Exists(projectDir))
        {
            return Fail($"Project directory does not exist: {projectDir}");
        }

        string outputPath = map.TryGetValue("--output", out string? outVal) && !string.IsNullOrWhiteSpace(outVal)
            ? Path.GetFullPath(outVal.Trim().Trim('"'))
            : Path.Combine(projectDir, "obj", "Generated", "LucideIconSvgMap.g.cs");

        HashSet<string> icons = await CollectIconsFromProject(projectDir, cancellationToken).ConfigureAwait(false);
        if (icons.Count == 0)
        {
            _logger.LogInformation("No LucideIcon usages found. Skipping LucideIconSvgMap generation.");
            return 0;
        }

        string? packageRoot = TryResolvePackageRoot();
        if (packageRoot == null)
        {
            _logger.LogWarning("Soenneker.Lucide.Icons package not found in NuGet cache. LucideIconSvgMap will have no SVG content.");
        }

        string content = GenerateLucideIconSvgMap(icons, packageRoot);
        string? outputDir = Path.GetDirectoryName(outputPath);
        if (!string.IsNullOrEmpty(outputDir))
        {
            Directory.CreateDirectory(outputDir);
        }

        await File.WriteAllTextAsync(outputPath, content, cancellationToken).ConfigureAwait(false);
        _logger.LogInformation("Generated {Output} with {Count} icons.", outputPath, icons.Count);
        return 0;
    }

    private static async Task<HashSet<string>> CollectIconsFromProject(string projectDir, CancellationToken ct)
    {
        var icons = new HashSet<string>(StringComparer.Ordinal);
        IEnumerable<string> csFiles = Directory.EnumerateFiles(projectDir, "*.cs", SearchOption.AllDirectories)
                                               .Concat(Directory.EnumerateFiles(projectDir, "*.razor", SearchOption.AllDirectories))
                                               .Where(p => !p.Contains("\\obj\\") && !p.Contains("/obj/") && !p.Contains("\\bin\\") && !p.Contains("/bin/"));

        foreach (string file in csFiles)
        {
            ct.ThrowIfCancellationRequested();
            string content;
            try
            {
                content = await File.ReadAllTextAsync(file, ct).ConfigureAwait(false);
            }
            catch
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

    private static string? TryResolvePackageRoot()
    {
        string packagesRoot = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            ".nuget",
            "packages");

        string packageDir = Path.Combine(packagesRoot, _packageId);
        if (!Directory.Exists(packageDir))
        {
            return null;
        }

        List<string?> versions = Directory.GetDirectories(packageDir)
                                          .Select(Path.GetFileName)
                                          .Where(n => !string.IsNullOrEmpty(n) && n![0] >= '0' && n[0] <= '9')
                                          .OrderByDescending(n => n, StringComparer.Ordinal)
                                          .ToList();

        return versions.Count > 0 ? Path.Combine(packageDir, versions[0]) : null;
    }

    private static string? ReadSvgContent(string packageRoot, string kebabIconName)
    {
        string path = Path.Combine(packageRoot, _resourcesSubPath, kebabIconName + ".svg");
        if (!File.Exists(path))
        {
            return null;
        }

        try
        {
            return File.ReadAllText(path);
        }
        catch
        {
            return null;
        }
    }

    private static string PascalToKebab(string pascal)
    {
        if (string.IsNullOrEmpty(pascal))
        {
            return pascal;
        }

        var sb = new StringBuilder(pascal.Length + 4);
        for (var i = 0; i < pascal.Length; i++)
        {
            char c = pascal[i];
            if (char.IsUpper(c))
            {
                if (i > 0)
                {
                    sb.Append('-');
                }
                sb.Append(char.ToLowerInvariant(c));
            }
            else
            {
                sb.Append(c);
            }
        }
        return sb.ToString();
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

    private string GenerateLucideIconSvgMap(HashSet<string> iconNames, string? packageRoot)
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
            string kebab = PascalToKebab(iconName);
            string? svgContent = packageRoot != null ? ReadSvgContent(packageRoot, kebab) : null;
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
