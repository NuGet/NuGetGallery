// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Globalization;
using System.IO;
using System.Threading.Tasks;
using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Blob;
using Microsoft.WindowsAzure.Storage.Blob.Protocol;
using Microsoft.WindowsAzure.Storage.RetryPolicies;
using Newtonsoft.Json;
using NuGetGallery.Auditing.Obfuscation;

namespace NuGetGallery.Auditing
{
    /// <summary>
    /// Writes audit records to a specific container in the Cloud Storage Account provided
    /// </summary>
    public class CloudAuditingService : AuditingService, ICloudStorageStatusDependency
    {
        public static readonly string DefaultContainerName = "auditing";

        private CloudBlobContainer _auditContainer;
        private string _instanceId;
        private string _localIP;
        private Func<Task<AuditActor>> _getOnBehalfOf;

        public CloudAuditingService(string instanceId, string localIP, string storageConnectionString, bool readAccessGeoRedundant, Func<Task<AuditActor>> getOnBehalfOf)
            : this(instanceId, localIP, GetContainer(storageConnectionString, readAccessGeoRedundant), getOnBehalfOf)
        {
        }

        public CloudAuditingService(string instanceId, string localIP, CloudBlobContainer auditContainer, Func<Task<AuditActor>> getOnBehalfOf)
        {
            _instanceId = instanceId;
            _localIP = localIP;
            _auditContainer = auditContainer;
            _getOnBehalfOf = getOnBehalfOf;
        }

        protected override async Task<AuditActor> GetActorAsync()
        {
            // Construct an actor representing the user the service is acting on behalf of
            AuditActor onBehalfOf = null;
            if(_getOnBehalfOf != null) {
                onBehalfOf = await _getOnBehalfOf();
            }
            return await AuditActor.GetCurrentMachineActorAsync(onBehalfOf);
        }

        protected override async Task SaveAuditRecordAsync(string auditData, string resourceType, string filePath, string action, DateTime timestamp)
        {
            string fullPath =
                $"{resourceType.ToLowerInvariant()}/" +
                $"{filePath.Replace(Path.DirectorySeparatorChar, '/')}/" +
                $"{Guid.NewGuid().ToString("N")}-{action.ToLowerInvariant()}.audit.v1.json";

            var blob = _auditContainer.GetBlockBlobReference(fullPath);
            bool retry = false;
            try
            {
                await WriteBlob(auditData, fullPath, blob);
            }
            catch (StorageException ex)
            {
                if (ex.RequestInformation?.ExtendedErrorInformation?.ErrorCode == BlobErrorCodeStrings.ContainerNotFound)
                {
                    retry = true;
                }
                else
                {
                    throw;
                }
            }

            if (retry)
            {
                // Create the container and try again,
                // this time we let exceptions bubble out
                await Task.Factory.FromAsync(
                    (cb, s) => _auditContainer.BeginCreateIfNotExists(cb, s),
                    ar => _auditContainer.EndCreateIfNotExists(ar),
                    null);
                await WriteBlob(auditData, fullPath, blob);
            }
        }

        private static CloudBlobContainer GetContainer(string storageConnectionString, bool readAccessGeoRedundant)
        {
            var cloudBlobClient = CloudStorageAccount.Parse(storageConnectionString).CreateCloudBlobClient();
            if (readAccessGeoRedundant)
            {
                cloudBlobClient.DefaultRequestOptions.LocationMode = LocationMode.PrimaryThenSecondary;
            }
            return cloudBlobClient.GetContainerReference(DefaultContainerName);
        }

        private static async Task WriteBlob(string auditData, string fullPath, CloudBlockBlob blob)
        {
            try
            {
                var strm = await Task.Factory.FromAsync(
                    (cb, s) => blob.BeginOpenWrite(
                        AccessCondition.GenerateIfNoneMatchCondition("*"),
                        new BlobRequestOptions(),
                        new OperationContext(),
                        cb, s),
                    ar => blob.EndOpenWrite(ar),
                    null);
                using (var writer = new StreamWriter(strm))
                {
                    await writer.WriteAsync(auditData);
                }
            }
            catch (StorageException ex)
            {
                if (ex.RequestInformation != null && ex.RequestInformation.HttpStatusCode == 409)
                {
                    // Blob already existed!
                    throw new InvalidOperationException(String.Format(
                        CultureInfo.CurrentCulture,
                        CoreStrings.CloudAuditingService_DuplicateAuditRecord,
                        fullPath), ex);
                }
                throw;
            }
        }

        public Task<bool> IsAvailableAsync(BlobRequestOptions options, OperationContext operationContext)
        {
            return _auditContainer.ExistsAsync(options, operationContext);
        }

        public override string RenderAuditEntry(AuditEntry entry)
        {
            if (entry == null)
            {
                throw new ArgumentNullException(nameof(entry));
            }

            var settings = GetJsonSerializerSettings();
            settings.Converters.Add(new ObfuscatorJsonConverter(entry));
            return JsonConvert.SerializeObject(entry, settings);
        }

        public override bool RecordWillBePersisted(AuditRecord auditRecord)
        {
            var packageAuditRecord = auditRecord as PackageAuditRecord;

            return packageAuditRecord != null &&
                (packageAuditRecord.Action == AuditedPackageAction.Delete || packageAuditRecord.Action == AuditedPackageAction.SoftDelete);
        }
    }
}