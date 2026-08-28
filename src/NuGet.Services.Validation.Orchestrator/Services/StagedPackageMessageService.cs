// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Threading.Tasks;
using NuGet.Services.Entities;

namespace NuGet.Services.Validation.Orchestrator
{
    public class StagedPackageMessageService : IMessageService<StagedPackage>
    {
        private readonly IMessageService<Package> _packageMessageService;

        public StagedPackageMessageService(IMessageService<Package> packageMessageService)
        {
            _packageMessageService = packageMessageService ?? throw new ArgumentNullException(nameof(packageMessageService));
        }

        public Task SendPublishedMessageAsync(StagedPackage stagedPackage)
        {
            stagedPackage = stagedPackage ?? throw new ArgumentNullException(nameof(stagedPackage));

            // TODO: Replace this with a staging-specific ready email.
            return _packageMessageService.SendPublishedMessageAsync(stagedPackage.Package);
        }

        public Task SendValidationFailedMessageAsync(StagedPackage stagedPackage, PackageValidationSet validationSet)
        {
            stagedPackage = stagedPackage ?? throw new ArgumentNullException(nameof(stagedPackage));

            // TODO: Replace this with a staging-specific validation failed email.
            return _packageMessageService.SendValidationFailedMessageAsync(stagedPackage.Package, validationSet);
        }

        public Task SendValidationTakingTooLongMessageAsync(StagedPackage stagedPackage)
        {
            stagedPackage = stagedPackage ?? throw new ArgumentNullException(nameof(stagedPackage));

            // TODO: Replace this with a staging-specific validation taking too long email.
            return _packageMessageService.SendValidationTakingTooLongMessageAsync(stagedPackage.Package);
        }
    }
}
