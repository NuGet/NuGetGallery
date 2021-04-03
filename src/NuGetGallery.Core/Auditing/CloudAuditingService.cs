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

        private Func<ICloudBlobContainer> _auditContainerFactory;
        private Func<Task<AuditActor>> _getOnBehalfOf;

        public CloudAuditingService(Func<ICloudBlobClient> cloudBlobClientFactory, Func<Task<AuditActor>> getOnBehalfOf)
            : this(() => GetContainer(cloudBlobClientFactory), getOnBehalfOf)
        {
        }

        public CloudAuditingService(Func<ICloudBlobContainer> auditContainerFactory, Func<Task<AuditActor>> getOnBehalfOf)
        {
            _auditContainerFactory = auditContainerFactory;
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

            var container = _auditContainerFactory();
            var blob = container.GetBlobReference(fullPath);
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
                var permissions = new BlobContainerPermissions { PublicAccess = BlobContainerPublicAccessType.Off };
                await container.CreateIfNotExistAsync(permissions);
                await WriteBlob(auditData, fullPath, blob);
            }
        }

        private static ICloudBlobContainer GetContainer(Func<ICloudBlobClient> cloudBlobClientFactory)
        {
            var cloudBlobClient = cloudBlobClientFactory();
            return cloudBlobClient.GetContainerReference(DefaultContainerName);
        }

        private static async Task WriteBlob(string auditData, string fullPath, ISimpleCloudBlob blob)
        {
            try
            {
                using (var stream = await blob.OpenWriteAsync(AccessCondition.GenerateIfNoneMatchCondition("*")))
                using (var writer = new StreamWriter(stream))
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
            return _auditContainerFactory().ExistsAsync(options, operationContext);
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