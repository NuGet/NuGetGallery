// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NuGet.Services.Entities;
using NuGet.Services.Validation.Orchestrator.Telemetry;

namespace NuGet.Services.Validation.Orchestrator
{
    public class PackageValidationMessageHandler : BaseValidationMessageHandler<Package>
    {
        public PackageValidationMessageHandler(
            IOptionsSnapshot<ValidationConfiguration> validationConfigsAccessor,
            IEntityService<Package> entityService,
            IValidationSetProvider<Package> validationSetProvider,
            IValidationSetProcessor validationSetProcessor,
            IValidationOutcomeProcessor<Package> validationOutcomeProcessor,
            ITelemetryService telemetryService,
            ILogger<PackageValidationMessageHandler> logger) : base(
                validationConfigsAccessor,
                entityService,
                validationSetProvider,
                validationSetProcessor,
                validationOutcomeProcessor,
                telemetryService,
                logger)
        {
        }

        protected override ValidatingType ValidatingType => ValidatingType.Package;
        protected override bool ShouldNoOpDueToDeletion => true;
    }
}
