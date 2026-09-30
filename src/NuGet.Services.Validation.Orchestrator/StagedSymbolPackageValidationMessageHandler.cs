// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NuGet.Jobs.Validation;
using NuGet.Jobs.Validation.Leases;
using NuGet.Services.Entities;
using NuGet.Services.Validation.Orchestrator.Telemetry;

namespace NuGet.Services.Validation.Orchestrator
{
    /// <summary>
    /// Processes validation messages for immutable staged symbol attempts.
    /// </summary>
    public class StagedSymbolPackageValidationMessageHandler : BaseValidationMessageHandler<StagedSymbolPackage>
    {
        private readonly IValidationOutcomeProcessor<StagedSymbolPackage> _outcomeProcessor;

        public StagedSymbolPackageValidationMessageHandler(
            IOptionsSnapshot<ValidationConfiguration> validationConfigsAccessor,
            IEntityService<StagedSymbolPackage> entityService,
            IValidationSetProvider<StagedSymbolPackage> validationSetProvider,
            IValidationSetProcessor validationSetProcessor,
            IValidationOutcomeProcessor<StagedSymbolPackage> validationOutcomeProcessor,
            IValidationStorageService validationStorageService,
            ILeaseService leaseService,
            IPackageValidationEnqueuer validationEnqueuer,
            IFeatureFlagService featureFlagService,
            ITelemetryService telemetryService,
            ILogger<StagedSymbolPackageValidationMessageHandler> logger)
            : base(validationConfigsAccessor, entityService, validationSetProvider, validationSetProcessor, validationOutcomeProcessor,
                  validationStorageService, leaseService, validationEnqueuer, featureFlagService, telemetryService, logger)
        {
            _outcomeProcessor = validationOutcomeProcessor;
        }

        protected override ValidatingType ValidatingType => ValidatingType.StagedSymbolPackage;

        protected override Task OnCompletedValidationSetAsync(PackageValidationSet validationSet, IValidatingEntity<StagedSymbolPackage> entity)
        {
            if (SymbolPromotionValidationConfiguration.IsPromotion(validationSet))
            {
                return _outcomeProcessor.ProcessValidationOutcomeAsync(validationSet, entity, new ValidationSetProcessorResult(), scheduleNextCheck: false);
            }

            return Task.CompletedTask;
        }
    }
}
