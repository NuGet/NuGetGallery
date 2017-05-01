// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.WindowsAzure.Storage;
using NuGet.Jobs.Validation.Common;
using NuGet.Jobs.Validation.Common.OData;
using NuGet.Jobs.Validation.Common.Validators.Vcs;

namespace NuGet.Jobs.Validation.Helper
{
    internal class Rescan : ICommand
    {
        private readonly ILogger<Rescan> _logger;
        private readonly NuGetV2Feed _feed;
        private readonly string _containerName;
        private readonly CloudStorageAccount _cloudStorageAccount;
        private readonly string _packageId;
        private readonly string _packageVersion;
        private readonly PackageValidationService _packageValidationService;
        private readonly string _galleryBaseAddress;

        public Action Action => Helper.Action.Rescan;

        public Rescan(
            IDictionary<string, string> jobArgsDictionary, 
            ILogger<Rescan> logger, 
            CloudStorageAccount cloudStorageAccount,
            string containerName,
            NuGetV2Feed feed, 
            PackageValidationService packageValidationService,
            string galleryBaseAddress
            )
        {
            _logger = logger;
            _feed = feed;

            _containerName = containerName;
            _cloudStorageAccount = cloudStorageAccount;

            _packageId = JobConfigurationManager.GetArgument(jobArgsDictionary, CommandLineArguments.PackageId);
            _packageVersion = JobConfigurationManager.GetArgument(jobArgsDictionary, CommandLineArguments.PackageVersion);
            _packageValidationService = packageValidationService;
            _galleryBaseAddress = galleryBaseAddress;
        }

        public async Task<bool> Run()
        {
            _logger.LogInformation($"Creating rescan request for {{{TraceConstant.PackageId}}} " +
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
