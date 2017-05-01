// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.WindowsAzure.Storage;
using NuGet.Jobs.Validation.Common;
using NuGet.Jobs.Validation.Common.OData;
using NuGet.Jobs.Validation.Common.Validators.Vcs;

namespace NuGet.Jobs.Validation.Helper
{
    internal class MarkClean : ICommand
    {
        private readonly ILogger<MarkClean> _logger;
        private readonly CloudStorageAccount _cloudStorageAccount;
        private readonly string _containerName;
        private readonly string _packageId;
        private readonly string _packageVersion;
        private readonly Guid _validationId;
        private readonly string _comment;
        private readonly string _alias;
        private readonly NuGetV2Feed _feed;
        private readonly PackageValidationAuditor _packageValidationAuditor;
        private readonly string _galleryBaseAddress;

        public Action Action => Helper.Action.MarkClean;

        public MarkClean(
            IDictionary<string, string> jobArgsDictionary,
            ILogger<MarkClean> logger,
            CloudStorageAccount cloudStorageAccount,
            string containerName,
            NuGetV2Feed feed,
            PackageValidationAuditor packageValidationAuditor,
            string galleryBaseAddress
            )
        {
            _logger = logger;
            _cloudStorageAccount = cloudStorageAccount;
            _containerName = containerName;

            _packageId = JobConfigurationManager.GetArgument(jobArgsDictionary, CommandLineArguments.PackageId);
            _packageVersion = JobConfigurationManager.GetArgument(jobArgsDictionary, CommandLineArguments.PackageVersion);
            var validationIdStr = JobConfigurationManager.GetArgument(jobArgsDictionary, CommandLineArguments.ValidationId);
            _validationId = Guid.Parse(validationIdStr);
            _comment = JobConfigurationManager.GetArgument(jobArgsDictionary, CommandLineArguments.Comment);
            _alias = JobConfigurationManager.GetArgument(jobArgsDictionary, CommandLineArguments.Alias);
            _feed = feed;
            _packageValidationAuditor = packageValidationAuditor;
            _galleryBaseAddress = galleryBaseAddress;
        }

        public async Task<bool> Run()
        {

            _logger.LogInformation($"Starting creating successful scan entry for the {{{TraceConstant.PackageId}}} " +
                    $"{{{TraceConstant.PackageVersion}}}",
                _packageId,
                _packageVersion);

            NuGetPackage package = await Util.GetPackage(_galleryBaseAddress, _feed, _packageId, _packageVersion);
            if (package == null)
            {
                _logger.LogError($"Unable to find {{{TraceConstant.PackageId}}} " +
                        $"{{{TraceConstant.PackageVersion}}}. Terminating.",
                    _packageId,
                    _packageVersion);
                return false;
            }
            _logger.LogInformation($"Found package {{{TraceConstant.PackageId}}} " +
                    $"{{{TraceConstant.PackageVersion}}}",
                package.Id,
                package.Version);

            string packageVersion = package.GetVersion();

            PackageValidationAuditEntry[] entries = new[] {new PackageValidationAuditEntry {
                Timestamp = DateTimeOffset.UtcNow,
                ValidatorName = VcsValidator.ValidatorName,
                Message = $"{_alias} marked the package as scanned clean, comment: {_comment}",
                EventId = ValidationEvent.PackageClean,
            }};

            _logger.LogInformation($"Marking the {{{TraceConstant.PackageId}}} " +
                    $"{{{TraceConstant.PackageVersion}}} " +
                    $"as clean with comment: {{{TraceConstant.Comment}}}. " +
                    $"Requested by {{{TraceConstant.Alias}}}",
                package.Id,
                packageVersion,
                _comment,
                _alias);
            await _packageValidationAuditor.WriteAuditEntriesAsync(_validationId, package.Id, packageVersion, entries);
            return true;
        }
    }
}
