// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Globalization;
using System.IO;
using System.Threading.Tasks;
using System.Web;
using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Blob;
using Microsoft.WindowsAzure.Storage.Blob.Protocol;

namespace NuGetGallery.Auditing
{
    /// <summary>
    /// Writes audit records to a specific container in the Cloud Storage Account provided
    /// </summary>
    public class CloudAuditingService : AuditingService
    {
        public static readonly string DefaultContainerName = "auditing";

        private readonly CloudBlobContainer _auditContainer;
        private readonly Func<AuditActor> _onBehalfOfThunk;

        public CloudAuditingService(string storageConnectionString, Func<AuditActor> onBehalfOfThunk)
            : this(GetContainer(storageConnectionString), onBehalfOfThunk)
        {

        }

        public CloudAuditingService(CloudBlobContainer auditContainer, Func<AuditActor> onBehalfOfThunk)
        {
            _auditContainer = auditContainer;
            _onBehalfOfThunk = onBehalfOfThunk;
        }

        public static AuditActor AspNetActorThunk()
        {
            // Use HttpContext to build an actor representing the user performing the action
            var context = HttpContext.Current;
            if (context == null)
            {
                return null;
            }

            string user = null;
            string authType = null;
            if (context.User != null)
            {
                user = context.User.Identity.Name;
                authType = context.User.Identity.AuthenticationType;
            }

            return new AuditActor(user, authType);
        }

        protected override AuditActor GetActor()
        {
            // Construct an actor representing the user the service is acting on behalf of
            AuditActor onBehalfOf = null;
            if (_onBehalfOfThunk != null)
            {
                onBehalfOf = _onBehalfOfThunk();
            }
            return AuditActor.GetCurrentMachineActor(onBehalfOf);
        }

        protected override async Task<Uri> SaveAuditRecord(string auditData, string resourceType, string filePath, string action, DateTime timestamp)
        {
            string fullPath = String.Concat(
                resourceType, "/",
                filePath.Replace(Path.DirectorySeparatorChar, '/'), "/",
                timestamp.ToString("s"), "-", // Sortable DateTime format
                action.ToLowerInvariant(), ".audit.v1.json");

            var blob = _auditContainer.GetBlockBlobReference(fullPath);
            bool retry = false;
            try
            {
                await WriteBlob(auditData, fullPath, blob);
            }
            catch (StorageException ex)
            {
                if (ex.RequestInformation != null &&
                    ex.RequestInformation.ExtendedErrorInformation != null &&
                    ex.RequestInformation.ExtendedErrorInformation.ErrorCode == BlobErrorCodeStrings.ContainerNotFound)
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

            return blob.Uri;
        }

        private static CloudBlobContainer GetContainer(string storageConnectionString)
        {
            return CloudStorageAccount.Parse(storageConnectionString)
                .CreateCloudBlobClient()
                .GetContainerReference(DefaultContainerName);
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
                        Strings.CloudAuditingService_DuplicateAuditRecord,
                        fullPath), ex);
                }
                throw;
            }
        }
    }
}
