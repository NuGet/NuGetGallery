using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Blob;
using System.Collections.Generic;
using System.Data.SqlClient;

namespace NuGet.Jobs
{
    public class SqlExportArguments
    {
        public string ConnectionString { get; private set; }
        public CloudStorageAccount Destination { get; private set; }
        public CloudBlobContainer DestinationContainer { get; private set; }
        public string DestinationContainerName { get; private set; }
        public string OutputDirectory { get; private set; }
        public string Name { get; private set; }

        public SqlExportArguments(IDictionary<string, string> jobArgsDictionary, string defaultContainerName, string defaultName)
        {
            var connStrBldr = new SqlConnectionStringBuilder(
                JobConfigurationManager.GetArgument(jobArgsDictionary, JobArgumentNames.SourceDatabase, EnvironmentVariableKeys.SqlGallery));

            ConnectionString = connStrBldr.ToString();
            OutputDirectory = JobConfigurationManager.GetArgument(jobArgsDictionary, JobArgumentNames.OutputDirectory);

            if (string.IsNullOrEmpty(OutputDirectory))
            {
                Destination = CloudStorageAccount.Parse(
                    JobConfigurationManager.GetArgument(jobArgsDictionary, JobArgumentNames.PrimaryDestination, EnvironmentVariableKeys.StoragePrimary));

                DestinationContainerName = JobConfigurationManager.TryGetArgument(jobArgsDictionary, JobArgumentNames.DestinationContainerName) ?? defaultContainerName;
                DestinationContainer = Destination.CreateCloudBlobClient().GetContainerReference(DestinationContainerName);
            }

            Name = defaultName;
        }
    }
}
