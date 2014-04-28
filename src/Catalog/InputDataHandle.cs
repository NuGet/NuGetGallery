using System.Threading.Tasks;
using VDS.RDF;

namespace Catalog
{
    public interface IInputDataHandle
    {
        Task<IGraph> CreateGraph(string baseAddress);
    }
}
