using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Azure;

namespace ObfuscateAuditLogs
{
    class Program
    {
        static void Main(string[] args)
        {
            //string runId = args?[0]; //?"runProd_5"
            //SingleRun(runId);

            //Run(19,25);

            //Test();
            string runId = "runProd_needprocessing_1";
            string conectionStringFrom = CloudConfigurationManager.GetSetting("ProdConnString");
            //string conectionStringTo = CloudConfigurationManager.GetSetting("IntConnString");
            if (string.IsNullOrEmpty(runId))
            {
                throw new ArgumentException("Enter valid run id as argument");
            }

            Console.WriteLine("Runnig {0}", runId);
            string containerFrom = "auditing";  //"devsecbakauditing";// "auditing";
            string folder = "package";
            AzureAuditProcessor processor = new AzureAuditProcessor(
                                                    connectionStringFrom: conectionStringFrom,
                                                    containerFrom: containerFrom,
                                                    connectionStringTo: conectionStringFrom,
                                                    containerTo: containerFrom,
                                                    executionRunId: runId);
            processor.MaxDateToUpate = new DateTimeOffset(2018, 1, 9, 12, 0, 0, TimeSpan.Zero);

            //ReprocessListOfFiles(processor, @"F:\NuGet\GDPR\AuditUpdates\ProdDeserializeErrorFiles2.txt");
            processor.LogFilesToBeProccesed(folder);
        }

        public static void Test()
        {

            string badFile = @"F:\NuGet\GDPR\AuditUpdates\BadFiles\2015-10-19T11_23_54-deleted.audit.v1.json";
            string conectionStringFrom = CloudConfigurationManager.GetSetting("TestConnectionString");

            AzureAuditProcessor p = new AzureAuditProcessor(conectionStringFrom);
            p.TestDeserialization(badFile);

            Console.WriteLine("End test.");
        }

        public static void SingleRun(string runId)
        {
            string conectionStringFrom = CloudConfigurationManager.GetSetting("ProdConnString");
            string conectionStringTo = CloudConfigurationManager.GetSetting("ProdConnString");
            if (string.IsNullOrEmpty(runId))
            {
                throw new ArgumentException("Enter valid run id as argument");
            }

            Console.WriteLine("Runnig {0}", runId);
            string containerFrom = "auditing";  //"devsecbakauditing";// "auditing";
            string containerTo = "auditing"; //"devsecbakauditing";// "auditing";
            string folder = "package";
            AzureAuditProcessor processor = new AzureAuditProcessor(
                                                    connectionStringFrom: conectionStringFrom,
                                                    containerFrom: containerFrom,
                                                    connectionStringTo: conectionStringTo,
                                                    containerTo: containerTo,
                                                    executionRunId: runId);

            processor.MaxDateToUpate = new DateTimeOffset(2018, 1, 9, 12, 0, 0, TimeSpan.Zero);
            processor.TryProcessFolderSegmented(folder, CancellationToken.None);
            //processor.PrintCountOfFilesToBeProccesed(folder);
            //processor.PrintDifferenceBetweenFolders(folder);
        }

        public static void ReprocessListOfFiles(AzureAuditProcessor processor, string pathToFile)
        {
            string[] files = File.ReadAllLines(pathToFile);
            processor.TryProcessListSegmented(files, CancellationToken.None);
        }


        public static void Run(int indexstart, int indexend)
        {
            string conectionStringFrom = CloudConfigurationManager.GetSetting("ProdConnString");
            string conectionStringTo = CloudConfigurationManager.GetSetting("ProdConnString");
            string containerFrom = "auditing";  
            string containerTo = "auditing"; 
            string folder = "package";
            HashSet<string> ignoredBlobs = GetIgnoredBlobs();
            AzureAuditProcessor processor = new AzureAuditProcessor(
                                                        connectionStringFrom: conectionStringFrom,
                                                        containerFrom: containerFrom,
                                                        connectionStringTo: conectionStringTo,
                                                        containerTo: containerTo,
                                                        executionRunId: "DefaultRun",
                                                        blobExclusionList: ignoredBlobs);

            processor.MaxDateToUpate = new DateTimeOffset(2018, 1, 9, 12, 0, 0, TimeSpan.Zero);

            for (int i = indexstart; i<indexend; i++)
            {
                string runId = $"runProd_{i}";
                Console.WriteLine("Runnig {0}", runId);

                processor.RunId = runId;
                processor.TryProcessFolderSegmented(folder, CancellationToken.None);

                Task.Delay(1000 * 60 * 2).Wait();
            }

        }

        static HashSet<string> GetIgnoredBlobs()
        {
            string ignoreBlobSetFile = @"F:\NuGet\GDPR\AuditUpdates\ProdDeserializeErrorFiles.txt";
            if(!File.Exists(ignoreBlobSetFile))
            {
                return new HashSet<string>();
            }
            return new HashSet<string>(File.ReadAllLines(ignoreBlobSetFile).Select(b => b.TrimEnd().TrimStart()));
        }
    }
}
