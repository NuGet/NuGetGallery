// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Threading.Tasks;

namespace NuGetGallery
{
    public class ValidationService : IValidationService
    {
        private readonly IPackageService _packageService;
        private readonly IPackageValidationInitiator _initiator;

        public ValidationService(
            IPackageService packageService,
            IPackageValidationInitiator initiator)
        {
            _packageService = packageService ?? throw new ArgumentNullException(nameof(packageService));
            _initiator = initiator ?? throw new ArgumentNullException(nameof(initiator));
        }

        public async Task StartValidationAsync(Package package)
        {
            var packageStatus = await _initiator.StartValidationAsync(package);

            await _packageService.UpdatePackageStatusAsync(
                package,
                packageStatus,
                commitChanges: false);
        }
    }
}