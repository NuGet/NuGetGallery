using JsonLDIntegration;
using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Blob;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Packaging;
using System.Xml;
using System.Xml.Linq;
using System.Xml.XPath;
using System.Xml.Xsl;
using VDS.RDF;
using VDS.RDF.Parsing;

namespace MakeMetadata
{
    class Stage
    {
        public string TransformName { get; set; }
        public IDictionary<string, string> TransformArgs { get; set; }
        public string Frame { get; set; }
        public string Output { get; set; }
    }

    static class Utils
    {
        public static Package GetPackage(Stream stream)
        {
            Package package = Package.Open(stream);
            return package;
        }

        public static XDocument GetNuspec(Package package)
        {
            foreach (PackagePart part in package.GetParts())
            {
                if (part.Uri.ToString().EndsWith(".nuspec"))
                {
                    XDocument nuspec = XDocument.Load(part.GetStream());
                    return nuspec;
                }
            }
            throw new FileNotFoundException("nuspec");
        }

        public static XDocument NormalizeNuspecNamespace(XDocument original)
        {
            XDocument result = new XDocument();

            using (XmlWriter writer = result.CreateWriter())
            {
                XslCompiledTransform xslt = new XslCompiledTransform();
                xslt.Load(XmlReader.Create(new StreamReader("normalizeNuspecNamespace.xslt")));
                xslt.Transform(original.CreateReader(), writer);
            }

            return result;
        }

        static IXmlNamespaceResolver CreateNamespaceResolver()
        {
            string ng = "http://nuget.org/schema#";
            XmlNamespaceManager namespaceManager = new XmlNamespaceManager(new NameTable());
            namespaceManager.AddNamespace("ng", ng);
            return namespaceManager;
        }

        public static void SavePlan(XDocument plan, string connectionString, string publishContainer)
        {
            string planName = plan.Root.XPathSelectElement("/ng:ExecutionPlan", CreateNamespaceResolver()).Attribute("name").Value;

            MemoryStream stream = new MemoryStream();
            XmlWriter writer = XmlWriter.Create(stream);
            plan.WriteTo(writer);
            writer.Flush();
            stream.Seek(0, SeekOrigin.Begin);

            Publish(stream, "plans/" + planName + ".xml", "application/xml", connectionString, publishContainer);
        }

        public static XDocument CreateExecutionPlan(XDocument nuspec)
        {
            XslCompiledTransform transform = CreateTransform("nuspec2executionPlan.xslt");
            XDocument executionPlan = new XDocument();
            using (XmlWriter writer = executionPlan.CreateWriter())
            {
                transform.Transform(nuspec.CreateReader(), writer);
            }
            return executionPlan;
        }

        static List<Stage> GetStages(XDocument executionPlan)
        {
            IEnumerable<XElement> transforms = executionPlan.Root.XPathSelectElements("/ng:ExecutionPlan/ng:Stage", CreateNamespaceResolver());

            List<Stage> stages = new List<Stage>();

            foreach (XElement transform in transforms)
            {
                Stage stage = new Stage();
                stage.TransformName = transform.Attribute("transform").Value;
                stage.Frame = transform.Attribute("frame").Value;
                stage.Output = transform.Attribute("output").Value;

                stage.TransformArgs = new Dictionary<string, string>();
                foreach (var child in transform.Descendants())
                {
                    stage.TransformArgs.Add(child.Attribute("name").Value, child.Attribute("value").Value);
                }

                stages.Add(stage);
            }

            return stages;
        }

        static XslCompiledTransform CreateTransform(string path)
        {
            XslCompiledTransform transform = new XslCompiledTransform();
            transform.Load(XmlReader.Create(new StreamReader(path)));
            return transform;
        }

        public static IGraph GetDocument(string transformName, IDictionary<string, string> transformArgs, XDocument data, string baseAddress)
        {
            XslCompiledTransform transform = CreateTransform(transformName);

            XsltArgumentList arguments = new XsltArgumentList();
            arguments.AddParam("base", "", baseAddress);
            foreach (KeyValuePair<string, string> arg in transformArgs)
            {
                arguments.AddParam(arg.Key, "", arg.Value);
            }

            XDocument rdfxml = new XDocument();
            using (XmlWriter writer = rdfxml.CreateWriter())
            {
                transform.Transform(data.CreateReader(), arguments, writer);
            }

            RdfXmlParser rdfXmlParser = new RdfXmlParser();
            XmlDocument doc = new XmlDocument();
            doc.Load(rdfxml.CreateReader());
            IGraph graph = new Graph();
            rdfXmlParser.Load(graph, doc);

            return graph;
        }

        public static List<Tuple<IGraph, string, string>> GetDocuments(XDocument data, XDocument executionPlan, string baseAddress)
        {
            List<Stage> plan = GetStages(executionPlan);
            List<Tuple<IGraph, string, string>> documents = new List<Tuple<IGraph, string, string>>();
            foreach (Stage stage in plan)
            {
                documents.Add(new Tuple<IGraph, string, string>(GetDocument(stage.TransformName, stage.TransformArgs, data, baseAddress), stage.Frame, stage.Output));
            }
            return documents;
        }

        public static bool TryLoadGraph(string name, out IGraph graph, string connectionString, string publishContainer)
        {
            CloudStorageAccount account = CloudStorageAccount.Parse(connectionString);
            CloudBlobClient client = account.CreateCloudBlobClient();
            CloudBlobContainer container = client.GetContainerReference(publishContainer);

            if (container.CreateIfNotExists())
            {
                Console.WriteLine("Created '{0}' publish container", publishContainer);
            }

            CloudBlockBlob blob = container.GetBlockBlobReference(name);

            if (blob.Exists())
            {
                MemoryStream stream = new MemoryStream();
                blob.DownloadToStream(stream);

                stream.Seek(0, SeekOrigin.Begin);

                //RdfJsonParser rdfJsonParser = new RdfJsonParser();
                JsonLdReader reader = new JsonLdReader();
                graph = new Graph();
                reader.Load(graph, new StreamReader(stream));
                return true;
            }

            graph = null;
            return false;
        }

        public static void ProcessReceived(Action<Stream, string, string> process, string connectionString, string receivedContainer, string publishContainer)
        {
            CloudStorageAccount account = CloudStorageAccount.Parse(connectionString);
            CloudBlobClient client = account.CreateCloudBlobClient();
            CloudBlobContainer container = client.GetContainerReference(receivedContainer);

            if (container.CreateIfNotExists())
            {
                Console.WriteLine("Created '{0}' received container", receivedContainer);
            }

            foreach (IListBlobItem item in container.ListBlobs())
            {
                CloudBlockBlob blob = (CloudBlockBlob)item;
                string leaseId = blob.AcquireLease(TimeSpan.FromSeconds(30), null);
                try
                {
                    MemoryStream stream = new MemoryStream();

                    blob.DownloadToStream(stream);

                    stream.Seek(0, SeekOrigin.Begin);
                    process(stream, connectionString, publishContainer);

                    stream.Seek(0, SeekOrigin.Begin);
                    Publish(stream, "data/" + blob.Name, "application/octet-stream", connectionString, publishContainer);

                    blob.Delete(DeleteSnapshotsOption.None, new AccessCondition { LeaseId = leaseId });
                }
                catch (Exception e)
                {
                    Console.WriteLine(e.Message);
                    blob.ReleaseLease(new AccessCondition { LeaseId = leaseId });
                }
            }

            Console.WriteLine(".");
        }

        /*
        public static bool SaveGraph(Tuple<IGraph, string, string> graph, string connectionString)
        {
            StringBuilder sb = new StringBuilder();
            using (System.IO.TextWriter writer = new System.IO.StringWriter(sb))
            {
                RdfJsonWriter jsonWriter = new RdfJsonWriter();
                jsonWriter.PrettyPrintMode = true;
                jsonWriter.Save(graph.Item1, writer);
            }

            MemoryStream stream = new MemoryStream();
            using (StreamWriter writer = new StreamWriter(stream))
            {
                writer.Write(sb.ToString());
                writer.Flush();

                stream.Seek(0, SeekOrigin.Begin);
                Publish(stream, graph.Item3, "application/json", connectionString);
            }

            return true;
        }
        */

        public static bool SaveGraph(Tuple<IGraph, string, string> graph, string connectionString, string publishContainer)
        {
            JToken frame;
            using (JsonReader jsonReader = new JsonTextReader(new StreamReader(graph.Item2)))
            {
                frame = JToken.Load(jsonReader);
            }

            MemoryStream stream = new MemoryStream();
            using (StreamWriter writer = new StreamWriter(stream))
            {
                IRdfWriter rdfWriter = new JsonLdWriter { Frame = frame };
                rdfWriter.Save(graph.Item1, writer);
                
                writer.Flush();

                stream.Seek(0, SeekOrigin.Begin);
                Publish(stream, graph.Item3, "application/json", connectionString, publishContainer);
            }

            return true;
        }

        public static void PublishMetadata(List<Tuple<IGraph, string, string>> newGraphs, string connectionString, string publishContainer)
        {
            foreach (Tuple<IGraph, string, string> newGraph in newGraphs)
            {
                IGraph existingGraph;
                if (TryLoadGraph(newGraph.Item3, out existingGraph, connectionString, publishContainer))
                {
                    newGraph.Item1.Merge(existingGraph);
                }
               SaveGraph(newGraph, connectionString, publishContainer);
            }
        }

        public static void Publish(Stream stream, string name, string contentType, string connectionString, string publishContainer)
        {
            CloudStorageAccount account = CloudStorageAccount.Parse(connectionString);
            CloudBlobClient client = account.CreateCloudBlobClient();
            CloudBlobContainer container = client.GetContainerReference(publishContainer);

            if (container.CreateIfNotExists())
            {
                container.SetPermissions(new BlobContainerPermissions { PublicAccess = BlobContainerPublicAccessType.Blob });
            }

            CloudBlockBlob blob = container.GetBlockBlobReference(name);
            blob.Properties.ContentType = contentType;
            blob.Properties.CacheControl = "no-store";

            blob.UploadFromStream(stream);

            Console.WriteLine("published: {0}", name);
        }
    }
}
