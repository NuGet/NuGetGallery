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
    }
}
