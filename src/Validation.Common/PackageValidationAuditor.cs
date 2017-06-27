// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Blob;
using Newtonsoft.Json;
using NuGet.Jobs.Validation.Common.Extensions;

namespace NuGet.Jobs.Validation.Common
{
    public class PackageValidationAuditor
    {
        private readonly CloudBlobContainer _auditsContainer;
        private readonly ILogger<PackageValidationAuditor> _logger;

        public PackageValidationAuditor(CloudStorageAccount cloudStorageAccount, string containerNamePrefix, ILoggerFactory loggerFactory)
        {
            var cloudBlobClient = cloudStorageAccount.CreateCloudBlobClient();
            _auditsContainer = cloudBlobClient.GetContainerReference(containerNamePrefix + "-audit");
            _auditsContainer.CreateIfNotExists(BlobContainerPublicAccessType.Blob);
            _logger = loggerFactory.CreateLogger<PackageValidationAuditor>();
        }

        public async Task StartAuditAsync(Guid validationId, string[] validators, DateTimeOffset started, string packageId, string packageVersion, NuGetPackage package)
        {
            _logger.LogInformation("Start writing Start PackageValidationAudit for " +
                    $"validation {{{TraceConstant.ValidationId}}} " +
                    $"- package {{{TraceConstant.PackageId}}} " +
                    $"v. {{{TraceConstant.PackageVersion}}}...", 
                validationId,
                package.Id,
                packageVersion);

            var packageValidationAudit = new PackageValidationAudit();
            packageValidationAudit.ValidationId = validationId;
            packageValidationAudit.PackageId = packageId;
            packageValidationAudit.PackageVersion = packageVersion;
            packageValidationAudit.Package = package;
            packageValidationAudit.Started = started;
            packageValidationAudit.Validators = validators;

            await StoreAuditAsync(validationId, packageValidationAudit.PackageId, packageValidationAudit.PackageVersion,
                _ => packageValidationAudit);

            _logger.LogInformation("Finished writing Start PackageValidationAudit for " +
                    $"validation {{{TraceConstant.ValidationId}}} " +
                    $"- package {{{TraceConstant.PackageId}}} " +
                    $"v. {{{TraceConstant.PackageVersion}}}.", 
                validationId, 
                package.Id, 
                packageVersion);
        }

        public async Task WriteAuditEntryAsync(Guid validationId, string packageId, string packageVersion, PackageValidationAuditEntry entry)
        {
            await WriteAuditEntriesAsync(validationId, packageId, packageVersion, new[] { entry });
        }

        public async Task WriteAuditEntriesAsync(Guid validationId, string packageId, string packageVersion, IEnumerable<PackageValidationAuditEntry> entries)
        {
            _logger.LogInformation("Start writing AuditEntry PackageValidationAudit for " +
                    $"validation {{{TraceConstant.ValidationId}}} " +
                    $"- package {{{TraceConstant.PackageId}}} " +
                    $"v. {{{TraceConstant.PackageVersion}}}...", 
                validationId, 
                packageId, 
                packageVersion);

            await StoreAuditAsync(validationId, packageId, packageVersion,
                packageValidationAudit =>
                {
                    packageValidationAudit.Entries.AddRange(entries);
                    return packageValidationAudit;
                });

            _logger.LogInformation("Finished writing AuditEntry PackageValidationAudit for " +
                    $"validation {{{TraceConstant.ValidationId}}} " +
                    $"- package {{{TraceConstant.PackageId}}} " +
                    $"v. {{{TraceConstant.PackageVersion}}}.",
                validationId,
                packageId,
                packageVersion);
        }

        public async Task CompleteAuditAsync(Guid validationId, DateTimeOffset completed, string packageId, string packageVersion)
        {
            _logger.LogInformation("Start writing Complete PackageValidationAudit for " +
                    $"validation {{{TraceConstant.ValidationId}}} " +
                    $"- package {{{TraceConstant.PackageId}}} " +
                    $"v. {{{TraceConstant.PackageVersion}}}...", 
                validationId, 
                packageId, 
                packageVersion);

            await StoreAuditAsync(validationId, packageId, packageVersion, 
                packageValidationAudit =>
                {
                    packageValidationAudit.Completed = completed;
                    return packageValidationAudit;
                });

            _logger.LogInformation("Finished writing Complete PackageValidationAudit for " +
                    $"validation {{{TraceConstant.ValidationId}}} " +
                    $"- package {{{TraceConstant.PackageId}}} " +
                    $"v. {{{TraceConstant.PackageVersion}}}.",
                validationId,
                packageId,
                packageVersion);
        }

        public async Task StoreAuditAsync(Guid validationId, string packageId, string packageVersion, Func<PackageValidationAudit, PackageValidationAudit> updateAudit)
        {
            _logger.LogInformation($"Started updating audit blob for validation {{{TraceConstant.ValidationId}}} " +
                $"- package {{{TraceConstant.PackageId}}} " +
                $"{{{TraceConstant.PackageVersion}}}",
                validationId,
                packageId, 
                packageVersion);

            var blob = _auditsContainer.GetBlockBlobReference(
                         GenerateAuditFileName(
                             validationId,
                             packageId,
                             packageVersion));

            var leaseId = await blob.TryAcquireLeaseAsync(TimeSpan.FromSeconds(30), CancellationToken.None);
            _logger.LogInformation($"Got blob lease: {{LeaseId}} for validation {{{TraceConstant.ValidationId}}}", 
                leaseId,
                validationId);

            var accessCondition = !string.IsNullOrEmpty(leaseId)
                ? AccessCondition.GenerateLeaseCondition(leaseId)
                : null;

            PackageValidationAudit packageValidationAudit;

            if (await blob.ExistsAsync())
            {
                _logger.LogInformation($"Updating existing auditing blob for validation {{{TraceConstant.ValidationId}}}",
                    validationId);
                var json = await blob.DownloadTextAsync(Encoding.UTF8, accessCondition, null, null);
                packageValidationAudit = JsonConvert.DeserializeObject<PackageValidationAudit>(json);
                packageValidationAudit = updateAudit(packageValidationAudit);
            }
            else
            {
                _logger.LogInformation($"Creating new auditing blob for validation {{{TraceConstant.ValidationId}}}",
                    validationId);
                packageValidationAudit = updateAudit(null);
            }

            _logger.LogInformation($"Saving updated audit blob for validation {{{TraceConstant.ValidationId}}}",
                validationId);
            await blob.UploadTextAsync(JsonConvert.SerializeObject(packageValidationAudit), Encoding.UTF8, accessCondition, null, null);

            blob.Properties.ContentType = "application/json";
            _logger.LogInformation($"Setting blob properties for validation {{{TraceConstant.ValidationId}}}",
                validationId);
            await blob.SetPropertiesAsync(accessCondition, null, null);

            if (accessCondition != null)
            {
                try
                {
                    _logger.LogInformation($"Releasing lease for validation {{{TraceConstant.ValidationId}}}",
                        validationId);
                    await blob.ReleaseLeaseAsync(AccessCondition.GenerateLeaseCondition(leaseId));
                }
                catch (Exception e)
                {
                    // intentional, lease may already have been expired
                    _logger.LogInformation(
                        TraceEvent.AuditBlobLeaseReleaseFailed,
                        e,
                        $"Exception occurred while releasing the lease for validation {{{TraceConstant.ValidationId}}}",
                        validationId);
                }
            }
            _logger.LogInformation($"Completed updating audit blob for validation {{{TraceConstant.ValidationId}}}",
                validationId);
        }

        public async Task<PackageValidationAudit> ReadAuditAsync(Guid validationId, string packageId, string packageVersion)
        {
            var blob = _auditsContainer.GetBlockBlobReference(
                         GenerateAuditFileName(
                             validationId,
                             packageId,
                             packageVersion));
            
            if (await blob.ExistsAsync())
            {
                var json = await blob.DownloadTextAsync();
                return JsonConvert.DeserializeObject<PackageValidationAudit>(json);
            }
            else
            {
                return null;
            }
        }

        private static string GenerateAuditFileName(Guid validationId, string packageId, string packageVersion)
        {
            return $"{packageId}/{packageVersion}/{validationId}.json";
        }
    }
}
