using JsonLD.Core;
using JsonLDIntegration;
using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Blob;
using Newtonsoft.Json.Linq;
using System;
using System.IO;
using VDS.RDF;

namespace GatherMergeRewrite
{
    class Storage
    {
        //  save

        public static void SaveJson(string name, IGraph graph, JToken frame = null)
        {
            StringWriter writer = new StringWriter();
            IRdfWriter rdfWriter = new JsonLdWriter();
            rdfWriter.Save(graph, writer);
            writer.Flush();

            JToken flattened = JToken.Parse(writer.ToString());

            if (frame == null)
            {
                Save(flattened.ToString(), name, "application/json");
            }
            else
            {
                JObject framed = JsonLdProcessor.Frame(flattened, frame, new JsonLdOptions());
                JObject compacted = JsonLdProcessor.Compact(framed, framed["@context"], new JsonLdOptions());

                Save(compacted.ToString(), name, "application/json");
            }
        }

        public static void SaveHtml(string name, string html)
        {
            Save(html, name, "text/html");
        }

        public static void Save(string str, string name, string contentType)
        {
            MemoryStream stream = new MemoryStream();
            using (StreamWriter writer = new StreamWriter(stream))
            {
                writer.Write(str);
                writer.Flush();
                stream.Seek(0, SeekOrigin.Begin);
                Save(stream, name, contentType);
            }
        }

        public static void Save(Stream stream, string name, string contentType)
        {
            CloudStorageAccount account = CloudStorageAccount.Parse(Config.ConnectionString);
            CloudBlobClient client = account.CreateCloudBlobClient();
            CloudBlobContainer container = client.GetContainerReference(Config.Container);

            if (container.CreateIfNotExists())
            {
                container.SetPermissions(new BlobContainerPermissions { PublicAccess = BlobContainerPublicAccessType.Blob });

                Console.WriteLine("Created '{0}' publish container", Config.Container);
            }

            CloudBlockBlob blob = container.GetBlockBlobReference(name);
            blob.Properties.ContentType = contentType;
            blob.Properties.CacheControl = "no-store";  // no for production, just helps with debugging

            blob.UploadFromStream(stream);

            Console.WriteLine("published: {0}", name);
        }

        //  load

        public static IGraph LoadResourceGraph(string name)
        {
            IGraph graph;
            if (TryLoadGraph(name, out graph))
            {
                return graph;
            }
            return null;
        }

        public static bool TryLoadGraph(string name, out IGraph graph)
        {
            CloudStorageAccount account = CloudStorageAccount.Parse(Config.ConnectionString);
            CloudBlobClient client = account.CreateCloudBlobClient();
            CloudBlobContainer container = client.GetContainerReference(Config.Container);

            if (container.CreateIfNotExists())
            {
                container.SetPermissions(new BlobContainerPermissions { PublicAccess = BlobContainerPublicAccessType.Blob });

                Console.WriteLine("Created '{0}' publish container", Config.Container);
            }

            CloudBlockBlob blob = container.GetBlockBlobReference(name);

            if (blob.Exists())
            {
                MemoryStream stream = new MemoryStream();
                blob.DownloadToStream(stream);

                stream.Seek(0, SeekOrigin.Begin);

                StreamReader reader = new StreamReader(stream);
                JToken compacted = JToken.Parse(reader.ReadToEnd());
                JToken flattened = JsonLdProcessor.Flatten(compacted, new JsonLdOptions());

                IRdfReader rdfReader = new JsonLdReader();
                graph = new Graph();
                rdfReader.Load(graph, new StringReader(flattened.ToString()));
                return true;
            }

            graph = null;
            return false;
        }
    }
}
