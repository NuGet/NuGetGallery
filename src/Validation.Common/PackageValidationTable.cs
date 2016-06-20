// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Table;

namespace NuGet.Jobs.Validation.Common
{
    public class PackageValidationTable
    {
        private readonly CloudTable _validationTable;

        public PackageValidationTable(CloudStorageAccount cloudStorageAccount, string containerNamePrefix)
        {
            var cloudTableClient = cloudStorageAccount.CreateCloudTableClient();
            _validationTable = cloudTableClient.GetTableReference(containerNamePrefix + "validation");
            _validationTable.CreateIfNotExists();
        }

        public async Task StoreAsync(PackageValidationEntity entity)
        {
            await _validationTable.ExecuteAsync(TableOperation.InsertOrReplace(entity));
        }

        public async Task<PackageValidationEntity> GetValidationAsync(Guid validationId)
        {
            var result = await _validationTable.ExecuteQuerySegmentedAsync(
                new TableQuery<PackageValidationEntity>
                {
                    FilterString = $"RowKey eq '{validationId}'",
                    TakeCount = 1
                }, null);

            return result.FirstOrDefault();
        }

        public async Task<IReadOnlyCollection<PackageValidationEntity>> GetIncompleteValidationsAsync()
        {
            // Note: this method only checks the latest 5000 items in storage
            // Older validations that are incomplete are not returned (to avoid query timeout)

            var result = new List<PackageValidationEntity>();

            TableContinuationToken continuationToken = null;
            for (int i = 0; i < 5; i++)
            {
                var page = await _validationTable.ExecuteQuerySegmentedAsync(
                    new TableQuery<PackageValidationEntity>(), continuationToken);
                continuationToken = page.ContinuationToken;

                if (page.Results.Any())
                {
                    result.AddRange(page.Results);
                }

                if (continuationToken == null)
                {
                    break;
                }
            }

            return result;
        }
    }
}