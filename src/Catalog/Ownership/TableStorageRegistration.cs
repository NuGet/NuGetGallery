// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.
using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Table;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;

namespace NuGet.Services.Metadata.Catalog.Ownership
{
    public class TableStorageRegistration : IRegistration
    {
        const string OwnershipTableName = "ownership";
        const string AgreementTableName = "agreement";
        const string OwnerType = "Owner";
        const string PackageType = "Package";

        CloudStorageAccount _account;

        public TableStorageRegistration(CloudStorageAccount account)
        {
            _account = account;
        }

        public async Task AddOwner(OwnershipRegistration registration, OwnershipOwner owner)
        {
            CloudTableClient client = _account.CreateCloudTableClient();
            CloudTable table = client.GetTableReference(OwnershipTableName);
            TableOperation operation = TableOperation.InsertOrReplace(new TypedEntity(registration.GetKey(), owner.GetKey(), OwnerType));
            await table.ExecuteAsync(operation);
        }

        public async Task AddVersion(OwnershipRegistration registration, OwnershipOwner owner, string version)
        {
            CloudTableClient client = _account.CreateCloudTableClient();
            CloudTable table = client.GetTableReference(OwnershipTableName);
            TableBatchOperation batch = new TableBatchOperation();
            batch.InsertOrReplace(new TypedEntity(registration.GetKey(), owner.GetKey(), OwnerType));
            batch.InsertOrReplace(new TypedEntity(registration.GetKey(), version, PackageType));
            await table.ExecuteBatchAsync(batch);
        }

        public Task DisableTenant(string tenant)
        {
            throw new NotImplementedException();
        }

        public Task EnableTenant(string tenant)
        {
            throw new NotImplementedException();
        }

        public Task<IEnumerable<OwnershipOwner>> GetOwners(OwnershipRegistration registration)
        {
            throw new NotImplementedException();
        }

        public Task<IEnumerable<OwnershipRegistration>> GetRegistrations(OwnershipOwner owner)
        {
            throw new NotImplementedException();
        }

        public Task<IEnumerable<string>> GetVersions(OwnershipRegistration registration)
        {
            throw new NotImplementedException();
        }

        public async Task<AgreementRecord> GetAgreement(string agreement, string agreementVersion, ClaimsPrincipal claimsPrincipal)
        {
            CloudTableClient client = _account.CreateCloudTableClient();
            CloudTable table = client.GetTableReference(AgreementTableName);
           
            TableQuery<AgreementRecordEntity> query = new TableQuery<AgreementRecordEntity>()
                    .Where(TableQuery.CombineFilters(
                        TableQuery.GenerateFilterCondition("PartitionKey", QueryComparisons.Equal,  AgreementRecord.GetKey(claimsPrincipal.FindFirst(ClaimTypes.NameIdentifier).Value, agreement)),
                        TableOperators.And,
                        TableQuery.GenerateFilterCondition("RowKey", QueryComparisons.Equal, agreementVersion)));

            TableQuerySegment<AgreementRecordEntity> segment =
                await table.ExecuteQuerySegmentedAsync(query, new TableContinuationToken());

            if (segment.Results.Count > 0)
            {
                var entity = segment.Results.First();
                return entity.ToAgreementRecord();
            }

            return null;
        }

        public async Task<AgreementRecord> AcceptAgreement(string agreement, string agreementVersion, string email, ClaimsPrincipal claimsPrincipal)
        {
            var record = AgreementRecord.Create(claimsPrincipal, agreement, agreementVersion, email);

            CloudTableClient client = _account.CreateCloudTableClient();
            CloudTable table = client.GetTableReference(AgreementTableName);
            TableOperation operation = TableOperation.InsertOrReplace(AgreementRecordEntity.FromAgreementRecord(record));
            await table.ExecuteAsync(operation);

            return record;
        }

        public Task<bool> HasOwner(OwnershipRegistration registration, OwnershipOwner owner)
        {
            return ExistsAsync(_account, registration.GetKey(), owner.GetKey());
        }

        public Task<bool> HasRegistration(OwnershipRegistration registration)
        {
            return ExistsAsync(_account, registration.GetKey());
        }

        public Task<bool> HasTenantEnabled(string tenant)
        {
            return Task.FromResult(true);
        }

        public Task<bool> HasVersion(OwnershipRegistration registration, string version)
        {
            return ExistsAsync(_account, registration.GetKey(), version);
        }

        public Task Remove(OwnershipRegistration registration)
        {
            throw new NotImplementedException();
        }

        public Task RemoveOwner(OwnershipRegistration registration, OwnershipOwner owner)
        {
            throw new NotImplementedException();
        }

        public Task RemoveVersion(OwnershipRegistration registration, string version)
        {
            throw new NotImplementedException();
        }

        // initialization

        public static async Task Initialize(string connectionString)
        {
            CloudStorageAccount account = CloudStorageAccount.Parse(connectionString);
            CloudTableClient client = account.CreateCloudTableClient();
            CloudTable ownershipTable = client.GetTableReference(OwnershipTableName);
            CloudTable agreementTable = client.GetTableReference(AgreementTableName);
            await ownershipTable.CreateIfNotExistsAsync();
            await agreementTable.CreateIfNotExistsAsync();
        }

        // implementation helpers

        static async Task<bool> ExistsAsync(CloudStorageAccount account, string registration, string value = null)
        {
            CloudTableClient client = account.CreateCloudTableClient();

            TableQuery<TypedEntity> query;
            if (value == null)
            {
                query = new TableQuery<TypedEntity>()
                    .Where(TableQuery.GenerateFilterCondition("PartitionKey", QueryComparisons.Equal, registration));
            }
            else
            {
                query = new TableQuery<TypedEntity>()
                    .Where(TableQuery.CombineFilters(
                        TableQuery.GenerateFilterCondition("PartitionKey", QueryComparisons.Equal, registration),
                        TableOperators.And,
                        TableQuery.GenerateFilterCondition("RowKey", QueryComparisons.Equal, value)));
            }

            CloudTable table = client.GetTableReference(OwnershipTableName);

            TableQuerySegment<TypedEntity> segment = await table.ExecuteQuerySegmentedAsync(query, new TableContinuationToken());

            return (segment.Results.Count > 0);
        }

        class TypedEntity : TableEntity
        {
            public TypedEntity()
            {
            }
            public TypedEntity(string partitionKey, string rowKey, string entityType)
                : base(partitionKey, rowKey)
            {
                EntityType = entityType;
            }
            public string EntityType { get; set; }
        }

        class AgreementRecordEntity : TableEntity
        {
            public AgreementRecordEntity()
            {
            }

            public AgreementRecordEntity(string partitionKey, string rowKey)
                : base(partitionKey, rowKey)
            {
            }
            
            public string NameIdentifier { get; set; }
            public string Iss { get; set; }
            public string Email { get; set; }
            public string Agreement { get; set; }
            public string AgreementVersion { get; set; }
            public DateTime DateAccepted { get; set; }

            public AgreementRecord ToAgreementRecord()
            {
                return new AgreementRecord()
                {
                    NameIdentifier = NameIdentifier,
                    Iss = Iss,
                    Email = Email,
                    Agreement = Agreement,
                    AgreementVersion = AgreementVersion,
                    DateAccepted = DateAccepted
                };
            }

            public static AgreementRecordEntity FromAgreementRecord(AgreementRecord record)
            {
                return new AgreementRecordEntity(AgreementRecord.GetKey(record.NameIdentifier, record.Agreement), record.AgreementVersion)
                {
                    NameIdentifier = record.NameIdentifier,
                    Iss = record.Iss,
                    Email = record.Email,
                    Agreement = record.Agreement,
                    AgreementVersion = record.AgreementVersion,
                    DateAccepted = record.DateAccepted
                };
            }
        }
    }
}
