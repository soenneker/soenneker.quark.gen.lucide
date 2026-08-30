using System.Threading;
using System.Threading.Tasks;

namespace Soenneker.Quark.Gen.Lucide.BuildTasks.Abstract;

/// <summary>
/// Generates the Lucide SVG map and dependency-injection support for a consuming project.
/// </summary>
public interface ILucideGeneratorRunner
{
    /// <summary>
    /// Generates outputs for the project and arguments supplied to the build-task process.
    /// </summary>
    /// <param name="cancellationToken">Token used to cancel the operation.</param>
    /// <returns>The process exit code: zero on success; otherwise nonzero.</returns>
    ValueTask<int> Run(CancellationToken cancellationToken = default);
}
