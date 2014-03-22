using JsonLDIntegration;
using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Blob;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using VDS.RDF;

namespace GatherMergeRewrite
{
    class Storage
    {
        //  save

        public static void SaveJson(string name, IGraph graph, JToken frame = null)
        {
            MemoryStream stream = new MemoryStream();
            using (StreamWriter writer = new StreamWriter(stream))
            {
                IRdfWriter rdfWriter = new JsonLdWriter { Frame = frame };
                rdfWriter.Save(graph, writer);
                writer.Flush();
                stream.Seek(0, SeekOrigin.Begin);
                Publish(stream, name, "application/json");
            }
        }

        public static void SaveHtml(string name, string html)
        {
            MemoryStream stream = new MemoryStream();
            using (StreamWriter writer = new StreamWriter(stream))
            {
                writer.Write(html);
                writer.Flush();
                stream.Seek(0, SeekOrigin.Begin);
                Publish(stream, name, "text/html");
            }
        }

        public static void Publish(Stream stream, string name, string contentType)
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
            blob.Properties.CacheControl = "no-store";

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

                JsonLdReader reader = new JsonLdReader();
                graph = new Graph();
                reader.Load(graph, new StreamReader(stream));
                return true;
            }

            graph = null;
            return false;
        }
    }
}
