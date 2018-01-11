using System;
using System.Threading;
using Microsoft.Azure;

namespace ObfuscateAuditLogs
{
    class Program
    {
        static void Main(string[] args)
        {
            string conectionStringFrom = CloudConfigurationManager.GetSetting("FromStorageConnectionString");
            string conectionStringTo = CloudConfigurationManager.GetSetting("ToStorageConnectionString");
            string runId = "";
            string containerFrom = "";
            string containerTo = "";
            string folder = "";
            AzureAuditProcessor processor = new AzureAuditProcessor(
                                                    connectionStringFrom: conectionStringFrom,
                                                    containerFrom: containerFrom,
                                                    connectionStringTo: conectionStringTo,
                                                    containerTo: containerTo,
                                                    executionRunId: runId);

            processor.MaxDateToUpate = new DateTimeOffset(2018, 1, 8, 12, 0, 0, TimeSpan.Zero);
            processor.TryProcessFolder(folder, CancellationToken.None);

            //processor.PrintDifferenceBetweenFolders(folder);
        }
    }
}
