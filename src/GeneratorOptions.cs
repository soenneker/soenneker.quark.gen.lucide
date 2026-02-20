namespace Soenneker.Quark.Gen.Lucide;

/// <summary>
/// Options for the LucideGenerator generator. Extend as needed for your generator.
/// </summary>
public sealed class GeneratorOptions
{
    public static readonly GeneratorOptions Default = new();

    public bool EmitDiagnostics { get; set; }
}
