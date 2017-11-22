// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;
using System.Threading.Tasks;
using System.Web;
using Microsoft.Extensions.Logging;
using Microsoft.WindowsAzure.Storage;
using NuGet.Jobs.Validation.Common;
using NuGet.Jobs.Validation.Common.OData;
using NuGet.Jobs.Validation.Common.Validators.Vcs;

namespace NuGet.Jobs.Validation.Helper
{
    public class Rescan : ICommand
    {
        private readonly ILogger<Rescan> _logger;
        private readonly NuGetV2Feed _feed;
        private readonly string _containerName;
        private readonly CloudStorageAccount _cloudStorageAccount;
        private readonly PackageValidationService _packageValidationService;
        private readonly string _galleryBaseAddress;

        public Action Action => Action.Rescan;

        public string PackageId { get; }
        public string PackageVersion { get; }

        public Rescan(
            IDictionary<string, string> jobArgsDictionary, 
            ILogger<Rescan> logger, 
            CloudStorageAccount cloudStorageAccount,
            string containerName,
            NuGetV2Feed feed, 
            PackageValidationService packageValidationService,
            string galleryBaseAddress)
        {
            _logger = logger;
            _feed = feed;

            _containerName = containerName;
            _cloudStorageAccount = cloudStorageAccount;

            PackageId = JobConfigurationManager.GetArgument(jobArgsDictionary, CommandLineArguments.PackageId);
            PackageId = HttpUtility.UrlDecode(PackageId);
            PackageVersion = JobConfigurationManager.GetArgument(jobArgsDictionary, CommandLineArguments.PackageVersion);
            PackageVersion = HttpUtility.UrlDecode(PackageVersion);
            _packageValidationService = packageValidationService;
            _galleryBaseAddress = galleryBaseAddress;
        }

        public async Task<bool> Run()
        {
            _logger.LogInformation($"Creating rescan request for {{{TraceConstant.PackageId}}} " +
                    $"{{{TraceConstant.PackageVersion}}}",
                PackageId,
                PackageVersion);

            var package = await Util.GetPackage(
                _galleryBaseAddress,
                _feed,
                PackageId,
                PackageVersion,
                includeDownloadUrl: false);
            if (package == null)
            {
                _logger.LogError($"Unable to find {{{TraceConstant.PackageId}}} " +
                        $"{{{TraceConstant.PackageVersion}}}. Terminating.",
                    PackageId,
                    PackageVersion);
                return false;
            }
            _logger.LogInformation($"Found package {{{TraceConstant.PackageId}}} " +
                    $"{{{TraceConstant.PackageVersion}}}",
                package.Id,
                package.Version);

            _logger.LogInformation($"Submitting rescan request for {{{TraceConstant.PackageId}}} " +
                    $"{{{TraceConstant.PackageVersion}}}",
                package.Id,
                package.Version);
            await _packageValidationService.StartValidationProcessAsync(package, new[] { VcsValidator.ValidatorName });
            _logger.LogInformation($"Done submitting rescan request for {{{TraceConstant.PackageId}}} " +
                    $"{{{TraceConstant.PackageVersion}}}",
                package.Id,
                package.Version);

            return true;
        }
    }
}
