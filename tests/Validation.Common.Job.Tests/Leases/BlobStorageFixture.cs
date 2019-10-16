// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Blob;

namespace Validation.Common.Job.Tests.Leases
{
    public class BlobStorageFixture : IDisposable
    {
        private const string ConnectionStringName = "TestStorageConnectionString";

        public static string SkipReason
        {
            get
            {
                if (GetEnvironmentVariable(ConnectionStringName, required: false) == null)
                {
                    return $"The {ConnectionStringName} environment variable needs to be defined to run Azure Blob " +
                        $"Storage integration tests.";
                }

                return null;
            }
        }

        public BlobStorageFixture()
        {
            TestRunId = Guid.NewGuid().ToString();
            ConnectionString = GetEnvironmentVariable(ConnectionStringName, required: true);

            GetContainerReference().CreateIfNotExists();
        }

        public string TestRunId { get; }
        public string ConnectionString { get; }

        public void Dispose()
        {
            GetContainerReference().DeleteIfExists();
        }

        private CloudBlobContainer GetContainerReference()
        {
            var account = CloudStorageAccount.Parse(ConnectionString);
            var blobClient = account.CreateCloudBlobClient();
            var container = blobClient.GetContainerReference(TestRunId);
            return container;
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
    }
}
