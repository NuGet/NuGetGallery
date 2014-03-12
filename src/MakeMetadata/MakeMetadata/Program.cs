using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Blob;
using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Packaging;
using System.Threading;
using System.Xml.Linq;
using VDS.RDF;

namespace MakeMetadata
{
    public class Program
    {
        static void PublishPackage(Stream stream, string connectionString, string publishContainer)
        {
            CloudStorageAccount account = CloudStorageAccount.Parse(connectionString);

            UriBuilder uri = new UriBuilder();
            uri.Scheme = "http";
            uri.Host = account.BlobEndpoint.Host;
            uri.Path = account.BlobEndpoint.AbsolutePath;

            string baseAddress = String.Format("{0}{1}/", uri.Uri, publishContainer.TrimEnd('/'));


            // BEGIN

            // (1)

            Package package = Utils.GetPackage(stream);

            // (2)

            XDocument awkwardNuspec = Utils.GetNuspec(package);

            // (3)

            XDocument nuspec = Utils.NormalizeNuspecNamespace(awkwardNuspec);

            // (4)

            XDocument executionPlan = Utils.CreateExecutionPlan(nuspec);

            // (5)

            Utils.SavePlan(executionPlan, connectionString, publishContainer);

            // (6)

            List<Tuple<IGraph, string, string>> metadata = Utils.GetDocuments(nuspec, executionPlan, baseAddress);

            // (7)

            Utils.PublishMetadata(metadata, connectionString, publishContainer);

            // END
        }

        public static void Run(string connectionString, string receivedContainer, string publishContainer)
        {
            try
            {
                while (true)
                {
                    Utils.ProcessReceived(PublishPackage, connectionString, receivedContainer, publishContainer);
                    Thread.Sleep(3 * 1000);
                }
            }
            catch (Exception e)
            {
                Console.WriteLine(e.Message);
            }
        }

        static void Main(string[] args)
        {
            try
            {
                if (args.Length != 1)
                {
                    Console.WriteLine("Enter connection string");
                    return;
                }

                Run(args[0], "received", "pub");

                //Tests.Test6();
                //Tests.Test7();
                //Tests.Test8();
            }
            catch (Exception e)
            {
                Console.WriteLine(e.Message);
            }
        }
    }
}
