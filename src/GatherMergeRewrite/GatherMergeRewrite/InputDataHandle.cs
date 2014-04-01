using System.Threading.Tasks;
using VDS.RDF;

namespace GatherMergeRewrite
{
    public interface IInputDataHandle
    {
        Task<IGraph> CreateGraph(string baseAddress);
    }
}
