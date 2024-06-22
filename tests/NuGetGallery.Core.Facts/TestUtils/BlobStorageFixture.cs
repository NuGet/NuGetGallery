// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Linq;
using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Blob;

namespace NuGetGallery
{
    public class BlobStorageFixture : IDisposable
    {
        private const string ConnectionStringAName = "TestStorageConnectionStringA";
        private const string ConnectionStringBName = "TestStorageConnectionStringB";

        public static string SkipReason
        {
            get
            {
                if (GetEnvironmentVariable(ConnectionStringAName, required: false) == null
                    || GetEnvironmentVariable(ConnectionStringBName, required: false) == null)
                {
                    return $"Both the {ConnectionStringAName} and {ConnectionStringBName} environment variables " +
                        "need to be defined to run Azure Blob Storage integration tests.";
                }

                return null;
            }
        }

        public BlobStorageFixture()
        {
            TestRunId = Guid.NewGuid().ToString();
            PrefixA = TestRunId + "/a";
            PrefixB = TestRunId + "/b";
            ConnectionStringA = GetEnvironmentVariable(ConnectionStringAName, required: true);
            ConnectionStringB = GetEnvironmentVariable(ConnectionStringBName, required: true);
        }

        public string TestRunId { get; }
        public string PrefixA { get; }
        public string PrefixB { get; }
        public string ConnectionStringA { get; }
        public string ConnectionStringB { get; }

        public void Dispose()
        {
            DeleteTestBlobs(ConnectionStringA);
            DeleteTestBlobs(ConnectionStringB);
        }

        private static string GetEnvironmentVariable(string name, bool required)
        {
            var value = Environment.GetEnvironmentVariable(name);

            if (string.IsNullOrWhiteSpace(value))
            {
                if (required)
                {
                    throw new InvalidOperationException($"The environment variable '{name}' must be defined.");
                }
                else
                {
                    return null;
                }
            }

            return value;
        }

        private void DeleteTestBlobs(string connectionString)
        {
            var account = CloudStorageAccount.Parse(connectionString);
            var blobClient = account.CreateCloudBlobClient();
            var containers = blobClient.ListContainers();
            foreach (var container in containers)
            {
                var blobs = container.ListBlobs(
                    prefix: TestRunId.ToString(),
                    useFlatBlobListing: true);

                foreach (var blob in blobs.OfType<CloudBlockBlob>())
                {
                    blob.DeleteIfExists(DeleteSnapshotsOption.IncludeSnapshots);
                }
            }
        }
    }
}
