// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.WindowsAzure.Storage;

namespace NuGet.Jobs.Validation.Common
{
    public class PackageValidationService
    {
        private readonly PackageValidationTable _packageValidationTable;
        private readonly PackageValidationQueue _packageValidationQueue;
        private readonly PackageValidationAuditor _packageValidationAuditor;
        private readonly INotificationService _notificationService;

        public PackageValidationService(CloudStorageAccount cloudStorageAccount, string containerNamePrefix)
        {
            _packageValidationTable = new PackageValidationTable(cloudStorageAccount, containerNamePrefix);
            _packageValidationQueue = new PackageValidationQueue(cloudStorageAccount, containerNamePrefix);
            _packageValidationAuditor = new PackageValidationAuditor(cloudStorageAccount, containerNamePrefix);
            _notificationService = new NotificationService(cloudStorageAccount, containerNamePrefix);
        }

        public async Task StartValidationProcessAsync(NuGetPackage package, string[] validators)
        {
            var validationId = Guid.NewGuid();
            var packageId = package.Id;
            var packageVersion = package.NormalizedVersion ?? package.Version;
            var created = DateTimeOffset.UtcNow;

            Trace.TraceInformation(
                "Starting validation process for validation {0} - package {1} {2}...", 
                validationId, packageId, packageVersion);

            // Write a tracking entity
            await _packageValidationTable.StoreAsync(new PackageValidationEntity
            {
                ValidationId = validationId,
                PackageId = packageId,
                PackageVersion = packageVersion,
                RequestedValidators = string.Join(";", validators.OrderBy(v => v)),
                CompletedValidators = string.Empty,
                Created = created
            });

            // Enqueue validations
            foreach (var validator in validators)
            {
                var message = new PackageValidationMessage
                {
                    ValidationId = validationId,
                    PackageId = packageId,
                    PackageVersion = packageVersion,
                    Package = package
                };

                await _packageValidationQueue.EnqueueAsync(validator, message);
            }

            // Write audit entry so we can get all the nitty-gritty details on our validation process
            try
            {
                await _packageValidationAuditor.StartAuditAsync(validationId, validators, created, packageId, packageVersion, package);
            }
            catch (Exception ex)
            {
                var logMessage = $"Error while starting validation process for validation {validationId} - package {packageId} {packageVersion}: {ex.Message} {ex.StackTrace}";

                Trace.TraceError(logMessage);

                await _notificationService.SendNotificationAsync(
                    "exception",
                    "Error while starting validation process for validation",
                    logMessage);

                throw;
            }

            Trace.TraceInformation("Started validation process for validation {0} - package {1} {2}.", validationId, packageId, packageVersion);
        }
    }
}