using Newtonsoft.Json.Linq;
using System.IO;
using System.Threading.Tasks;
using VDS.RDF;
using VDS.RDF.Writing;

namespace GatherMergeRewrite
{
    static class Debug
    {
        public static void DumpTurtle(string filename, IGraph graph)
        {
            filename = filename.Replace('/', '_');

            CompressingTurtleWriter writer = new CompressingTurtleWriter();
            writer.Save(graph, filename);
        }

        public static async Task Dump(State state, PackageData data, IStorage storage)
        {
            string name = "debug/" + data.RegistrationId;
            IGraph graph = SparqlHelpers.Construct(state.Store, new StreamReader(Utils.GetResourceStream("sparql\\All.rq")).ReadToEnd());

            await storage.Save("application/json", name, Utils.CreateJson(graph));
        }
    }
}
