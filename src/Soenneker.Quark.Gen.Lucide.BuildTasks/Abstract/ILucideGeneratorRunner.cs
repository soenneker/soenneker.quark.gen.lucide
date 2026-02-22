using System.Threading;
using System.Threading.Tasks;

namespace Soenneker.Quark.Gen.Lucide.BuildTasks.Abstract;

public interface ILucideGeneratorRunner
{
    ValueTask<int> Run(CancellationToken cancellationToken = default);
}
