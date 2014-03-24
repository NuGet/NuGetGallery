using JsonLD.Core;
using JsonLDIntegration;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using VDS.RDF;
using VDS.RDF.Parsing;
using VDS.RDF.Writing;

namespace JsonLDIntegrationTests
{
    class Program
    {
        static void Test0()
        {
            IGraph g = new Graph();

            TurtleParser parser = new TurtleParser();
            parser.Load(g, "datatypes.test.ttl");

            System.IO.StringWriter stringWriter = new System.IO.StringWriter();

            JToken frame;
            using (JsonReader reader = new JsonTextReader(new StreamReader("datatypes.context.json")))
            {
                frame = JToken.Load(reader);
            }

            JsonLdWriter jsonLdWriter = new JsonLdWriter();
            jsonLdWriter.Save(g, stringWriter);

            JToken flattened = JToken.Parse(stringWriter.ToString());

            JObject framed = JsonLdProcessor.Frame(flattened, frame, new JsonLdOptions());
            JObject compacted = JsonLdProcessor.Compact(framed, framed["@context"], new JsonLdOptions());

            Console.WriteLine(compacted);

            JToken flattened2 = JsonLdProcessor.Flatten(compacted, new JsonLdOptions());

            IGraph g2 = new Graph();
            JsonLdReader jsonLdReader = new JsonLdReader();
            jsonLdReader.Load(g2, new StringReader(flattened2.ToString()));

            CompressingTurtleWriter turtleWriter = new CompressingTurtleWriter();

            turtleWriter.DefaultNamespaces.AddNamespace("ns", new Uri("http://tempuri.org/schema#"));

            turtleWriter.Save(g2, Console.Out);
        }

        static JToken ApplyFrame(IGraph g, string frameName)
        {
            JToken frame;
            using (JsonReader reader = new JsonTextReader(new StreamReader(frameName)))
            {
                frame = JToken.Load(reader);
            }

            System.IO.StringWriter stringWriter = new System.IO.StringWriter();

            JsonLdWriter jsonLdWriter = new JsonLdWriter();
            jsonLdWriter.Save(g, stringWriter);

            return JToken.Parse(stringWriter.ToString());
        }

        static void Test1()
        {
            IGraph g = new Graph();

            TurtleParser parser = new TurtleParser();
            parser.Load(g, "multitypes.data.ledzep.ttl");

            JToken jimmy = ApplyFrame(g, "multitypes.context.jimmy.json");
            JToken robert = ApplyFrame(g, "multitypes.context.robert.json");
            JToken johnpaul = ApplyFrame(g, "multitypes.context.johnpaul.json");
            JToken john = ApplyFrame(g, "multitypes.context.john.json");

            Console.WriteLine("-------- -------- ------- ------- --------");
            Console.WriteLine(jimmy);
            Console.WriteLine("-------- -------- ------- ------- --------");
            Console.WriteLine(robert);
            Console.WriteLine("-------- -------- ------- ------- --------");
            Console.WriteLine(johnpaul);
            Console.WriteLine("-------- -------- ------- ------- --------");
            Console.WriteLine(john);
        }

        static void Main(string[] args)
        {
            try
            {
                Test0();
                //Test1();
            }
            catch (Exception e)
            {
                Console.WriteLine(e.Message);
            }
        }
    }
}
