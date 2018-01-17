using System;
using System.Threading;
using Microsoft.Azure;

namespace ObfuscateAuditLogs
{
    class Program
    {
        static void Main(string[] args)
        {
            string conectionStringFrom = CloudConfigurationManager.GetSetting("StorageConnectionStringDev");//FromStorageConnectionString
            string conectionStringTo = CloudConfigurationManager.GetSetting("StorageConnectionStringDev");
            string runId = "runDev10_5";
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
            processor.TryProcessFolder(folder, CancellationToken.None);
            //processor.PrintCountOfFilesToBeProccesed(folder);
            //processor.PrintDifferenceBetweenFolders(folder);
        }
    }
}
