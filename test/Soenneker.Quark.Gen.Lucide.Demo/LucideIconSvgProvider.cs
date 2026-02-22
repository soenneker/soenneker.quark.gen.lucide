using Soenneker.Quark;
using Soenneker.Quark.Gen.Lucide.Generated;

namespace Soenneker.Quark.Gen.Lucide.Demo;

/// <summary>
/// Implements <see cref="ILucideIconSvgProvider"/> using the generated <see cref="LucideIconSvgMap"/>.
/// </summary>
public sealed class LucideIconSvgProvider : ILucideIconSvgProvider
{
    /// <inheritdoc />
    public string? GetSvg(string iconName)
    {
        return LucideIconSvgMap.GetSvg(iconName);
    }
}
