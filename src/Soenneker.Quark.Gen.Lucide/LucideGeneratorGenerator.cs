using Microsoft.CodeAnalysis;

namespace Soenneker.Quark.Gen.Lucide;

/// <summary>
/// Represents the lucide generator generator.
/// </summary>
[Generator]
public sealed class LucideGeneratorGenerator : IIncrementalGenerator
{
    /// <summary>
    /// Initializes the Lucide Generator Generator so it is ready for use.
    /// </summary>
    /// <param name="context">HTTP context containing the Authorization header.</param>
    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
    }
}
