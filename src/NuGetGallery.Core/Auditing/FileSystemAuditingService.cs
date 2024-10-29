// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.IO;
using System.Threading.Tasks;

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

        protected override async Task<AuditActor> GetActorAsync()
        {
            // Construct an actor representing the user the service is acting on behalf of
            AuditActor onBehalfOf = null;
            if (_getOnBehalfOf != null)
            {
                onBehalfOf = await _getOnBehalfOf();
            }

            return await AuditActor.GetCurrentMachineActorAsync(onBehalfOf);
        }

        protected override Task SaveAuditRecordAsync(string auditData, string resourceType, string filePath, string action, DateTime timestamp)
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

            return Task.FromResult(0);
        }
    }
}