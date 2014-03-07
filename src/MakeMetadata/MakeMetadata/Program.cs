using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Packaging;
using System.Threading;
using System.Xml.Linq;
using VDS.RDF;

namespace MakeMetadata
{
    class Program
    {
        static string ConnectionString;
        static string BaseAddress;

        static void PublishPackage(Stream stream)
        {
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

            Utils.SavePlan(executionPlan, ConnectionString);

            // (6)

            List<Tuple<IGraph, string, string>> metadata = Utils.GetDocuments(nuspec, executionPlan, BaseAddress);

            // (7)

            Utils.PublishMetadata(metadata, ConnectionString);

            // END
        }

        static void Main(string[] args)
        {
            try
            {
                if (args.Length != 2)
                {
                    Console.WriteLine("Enter connection string and base address");
                    return;
                }
                else
                {
                    ConnectionString = args[0];
                    BaseAddress = args[1];
                }

                //ProcessReceived(ProcessPackage);

                while (true)
                {
                    Utils.ProcessReceived(PublishPackage, ConnectionString);
                    Thread.Sleep(3 * 1000);
                }

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
