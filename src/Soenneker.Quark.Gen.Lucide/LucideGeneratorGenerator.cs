using Microsoft.CodeAnalysis;

namespace Soenneker.Quark.Gen.Lucide;

/// <summary>
/// Represents the lucide generator generator.
/// </summary>
[Generator]
public sealed class LucideGeneratorGenerator : IIncrementalGenerator
{
    /// <summary>
    /// Executes the initialize operation.
    /// </summary>
    /// <param name="context">The context.</param>
    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
    }
}
