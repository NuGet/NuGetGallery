// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;
using System.Data.SqlClient;
using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Blob;
using NuGet.Jobs;

namespace Search.GenerateAuxiliaryData
{
    internal class SqlExportArguments
    {
        public string ConnectionString { get; }
        public CloudStorageAccount Destination { get; }
        public CloudBlobContainer DestinationContainer { get; }
        public string DestinationContainerName { get; }
        public string Name { get; }

        public SqlExportArguments(IDictionary<string, string> jobArgsDictionary, string defaultContainerName, string defaultName)
        {
            var connStrBldr = new SqlConnectionStringBuilder(
                JobConfigurationManager.GetArgument(jobArgsDictionary, JobArgumentNames.PackageDatabase));

            ConnectionString = connStrBldr.ToString();

            Destination = CloudStorageAccount.Parse(
                    JobConfigurationManager.GetArgument(jobArgsDictionary, JobArgumentNames.PrimaryDestination));

            DestinationContainerName = JobConfigurationManager.TryGetArgument(jobArgsDictionary, JobArgumentNames.DestinationContainerName) ?? defaultContainerName;
            DestinationContainer = Destination.CreateCloudBlobClient().GetContainerReference(DestinationContainerName);

            Name = defaultName;
        }
    }
}
