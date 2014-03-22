using Newtonsoft.Json.Linq;
using System.IO;
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

        public static void Dump(State state, UploadData data)
        {
            string name = data.RegistrationId + ".all";
            IGraph graph = Utils.Construct(state.Store, new StreamReader("sparql\\All.rq").ReadToEnd());

            Storage.SaveJson(name, graph);
        }
    }
}
