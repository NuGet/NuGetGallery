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

        private CloudBlobContainer _auditContainer;
        private string _instanceId;
        private string _localIP;
        private Func<Task<AuditActor>> _getOnBehalfOf;

        public CloudAuditingService(string instanceId, string localIP, string storageConnectionString, Func<Task<AuditActor>> getOnBehalfOf)
            : this(instanceId, localIP, GetContainer(storageConnectionString), getOnBehalfOf)
        {

        }

        public CloudAuditingService(string instanceId, string localIP, CloudBlobContainer auditContainer, Func<Task<AuditActor>> getOnBehalfOf)
        {
            _instanceId = instanceId;
            _localIP = localIP;
            _auditContainer = auditContainer;
            _getOnBehalfOf = getOnBehalfOf;
        }

        public static Task<AuditActor> GetAspNetOnBehalfOf()
        {
            // Use HttpContext to build an actor representing the user performing the action
            var context = HttpContext.Current;
            if (context == null)
            {
                return null;
            }

            // Try to identify the client IP using various server variables
            string clientIpAddress = context.Request.ServerVariables["HTTP_X_FORWARDED_FOR"];
            if (string.IsNullOrEmpty(clientIpAddress)) // Try REMOTE_ADDR server variable
            {
                clientIpAddress = context.Request.ServerVariables["REMOTE_ADDR"];
            }

            if (string.IsNullOrEmpty(clientIpAddress)) // Try UserHostAddress property
            {
                clientIpAddress = context.Request.UserHostAddress;
            }

            if (!string.IsNullOrEmpty(clientIpAddress) && clientIpAddress.IndexOf(".", StringComparison.Ordinal) > 0)
            {
                clientIpAddress = clientIpAddress.Substring(0, clientIpAddress.LastIndexOf(".", StringComparison.Ordinal)) + ".0";
            }

            string user = null;
            string authType = null;
            if (context.User != null)
            {
                user = context.User.Identity.Name;
                authType = context.User.Identity.AuthenticationType;
            }

            return Task.FromResult(new AuditActor(
                null,
                clientIpAddress,
                user,
                authType,
                DateTime.UtcNow));
        }

        protected override async Task<AuditActor> GetActor()
        {
            // Construct an actor representing the user the service is acting on behalf of
            AuditActor onBehalfOf = null;
            if(_getOnBehalfOf != null) {
                onBehalfOf = await _getOnBehalfOf();
            }
            return await AuditActor.GetCurrentMachineActor(onBehalfOf);
        }

        protected override async Task<Uri> SaveAuditRecord(string auditData, string resourceType, string filePath, string action, DateTime timestamp)
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
                        CoreStrings.CloudAuditingService_DuplicateAuditRecord,
                        fullPath));
                }
                throw;
            }
        }
    }
}
