using System.Threading;
using System.Threading.Tasks;

namespace Soenneker.Quark.Gen.Lucide.BuildTasks.Abstract;

/// <summary>
/// Defines the lucide generator runner contract.
/// </summary>
public interface ILucideGeneratorRunner
{
    /// <summary>
    /// Runs lucide Generator Runner for the Lucide Generator Runner.
    /// </summary>
    /// <param name="cancellationToken">Token used to cancel the operation.</param>
    /// <returns>A task whose result is the requested value.</returns>
    ValueTask<int> Run(CancellationToken cancellationToken = default);
}
