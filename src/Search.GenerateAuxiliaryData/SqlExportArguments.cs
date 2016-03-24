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
        public string ConnectionString { get; private set; }
        public CloudStorageAccount Destination { get; private set; }
        public CloudBlobContainer DestinationContainer { get; private set; }
        public string DestinationContainerName { get; private set; }
        public string Name { get; private set; }

        public SqlExportArguments(IDictionary<string, string> jobArgsDictionary, string defaultContainerName, string defaultName)
        {
            var connStrBldr = new SqlConnectionStringBuilder(
                JobConfigurationManager.GetArgument(jobArgsDictionary, JobArgumentNames.PackageDatabase, EnvironmentVariableKeys.SqlGallery));

            ConnectionString = connStrBldr.ToString();

            Destination = CloudStorageAccount.Parse(
                    JobConfigurationManager.GetArgument(jobArgsDictionary, JobArgumentNames.PrimaryDestination, EnvironmentVariableKeys.StoragePrimary));

            DestinationContainerName = JobConfigurationManager.TryGetArgument(jobArgsDictionary, JobArgumentNames.DestinationContainerName) ?? defaultContainerName;
            DestinationContainer = Destination.CreateCloudBlobClient().GetContainerReference(DestinationContainerName);

            Name = defaultName;
        }
    }
}
