// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.IO;
using System.Threading.Tasks;
using System.Web;

namespace NuGetGallery.Auditing
{
    /// <summary>
    /// Writes audit records to a specific directory in the file system
    /// </summary>
    public class FileSystemAuditingService : AuditingService
    {
        public static readonly string DefaultContainerName = "auditing";

        private readonly string _auditingPath;
        private readonly Func<Task<AuditActor>> _getOnBehalfOf;
        
        public FileSystemAuditingService(string auditingPath, Func<Task<AuditActor>> getOnBehalfOf)
        {
            if (string.IsNullOrEmpty(auditingPath))
            {
                throw new ArgumentNullException(nameof(auditingPath));
            }
            
            if (getOnBehalfOf == null)
            {
                throw new ArgumentNullException(nameof(getOnBehalfOf));
            }

            _auditingPath = auditingPath;
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
            var clientIpAddress = context.Request.ServerVariables["HTTP_X_FORWARDED_FOR"];
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
            if (_getOnBehalfOf != null)
            {
                onBehalfOf = await _getOnBehalfOf();
            }

            return await AuditActor.GetCurrentMachineActor(onBehalfOf);
        }

        protected override Task<Uri> SaveAuditRecord(string auditData, string resourceType, string filePath, string action, DateTime timestamp)
        {
            // Build relative file path
            var relativeFilePath = 
               $"{resourceType.ToLowerInvariant()}{Path.DirectorySeparatorChar}" +
               $"{filePath}{Path.DirectorySeparatorChar}" +
               $"{Guid.NewGuid().ToString("N")}-{action.ToLowerInvariant()}.audit.v1.json";
            
            // Build full file path
            var fullFilePath = Path.Combine(_auditingPath, relativeFilePath);

            // Ensure the directory exists
            var directoryName = Path.GetDirectoryName(fullFilePath);
            if (!Directory.Exists(directoryName))
            {
                Directory.CreateDirectory(directoryName);
            }

            // Write the data
            File.WriteAllText(fullFilePath, auditData);

            // Generate a local URL
            var uri = new Uri($"https://auditing.local/{relativeFilePath.Replace(Path.DirectorySeparatorChar, '/')}");

            return Task.FromResult(uri);
        }
    }
}
