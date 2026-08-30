using Microsoft.CodeAnalysis;

namespace Soenneker.Quark.Gen.Lucide;

/// <summary>
/// Provides the analyzer entry point for the Lucide build-time generator package.
/// </summary>
[Generator]
public sealed class LucideGeneratorGenerator : IIncrementalGenerator
{
    /// <summary>
    /// Initializes the analyzer entry point. Lucide source generation is performed by the package's MSBuild task.
    /// </summary>
    /// <param name="context">The incremental generator initialization context.</param>
    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
    }
}
