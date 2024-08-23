// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using NuGet.Services.Entities;
using NuGet.Services.Validation.Orchestrator.Telemetry;

namespace NuGet.Services.Validation.Orchestrator
{
    public class SymbolsStatusProcessor : EntityStatusProcessor<SymbolPackage>
    {
        public SymbolsStatusProcessor(
            IEntityService<SymbolPackage> galleryPackageService,
            IValidationFileService packageFileService,
            IValidatorProvider validatorProvider,
            ITelemetryService telemetryService,
            ILogger<EntityStatusProcessor<SymbolPackage>> logger) 
            : base(galleryPackageService, packageFileService, validatorProvider, telemetryService, logger)
        {
        }

        protected override async Task MakePackageAvailableAsync(IValidatingEntity<SymbolPackage> validatingEntity, PackageValidationSet validationSet)
        {
            if(!CanProceedToMakePackageAvailable(validatingEntity, validationSet))
            {
                _logger.LogInformation("SymbolsPackage PackageId {PackageId} PackageVersion {PackageVersion} Status {Status} was not made available again.",
                    validationSet.PackageId,
                    validationSet.PackageNormalizedVersion,
                    validatingEntity.Status);
                return;
            }
            await base.MakePackageAvailableAsync(validatingEntity, validationSet);
        }

        /// <summary>
        /// Proceed to change the state only if:
        /// 1.the current symbol entity is in a failed state and there is not an existent symbol push already started by the user. This state can happen on revalidation only.
        /// or 
        /// 2. the current validation is in validating state
        /// If the symbols validation would have processors as validators the copy should be done on other states as well.
        /// </summary>
        /// <param name="validatingEntity">The <see cref="IValidatingEntity<SymbolPackage>"/> that is under current revalidation.</param>
        /// <param name="validationSet">The validation set for the current validation.</param>
        /// <returns>True if the package should be made available (copied to the public container, db updated etc.)</returns>
        public bool CanProceedToMakePackageAvailable(IValidatingEntity<SymbolPackage> validatingEntity, PackageValidationSet validationSet)
        {
            // _galleryPackageService.FindPackageByIdAndVersionStrict will return the symbol entity that is in Validating state or null if
            // there not any symbols entity in validating state.
            var validatingSymbolsEntity = _galleryPackageService.FindPackageByIdAndVersionStrict(validationSet.PackageId, validationSet.PackageNormalizedVersion);

            // If the current entity is in validating mode a new symbolPush is not allowed, so it is safe to copy.
            var aNewEntityInValidatingStateExists = validatingSymbolsEntity != null;

            var proceed = validatingEntity.Status == PackageStatus.Validating || (!aNewEntityInValidatingStateExists && validatingEntity.Status == PackageStatus.FailedValidation);
            _logger.LogInformation("Proceed to make symbols available check: "
                + "PackageId {PackageId} "
                + "PackageVersion {PackageVersion} "
                + "ValidationTrackingId {ValidationTrackingId} "
                + "CurrentValidating entity status {CurrentEntityStatus}"
                + "ANewEntityInValidatingStateExists {ANewEntityInValidatingStateExists}"
                + "Proceed {Proceed}",
                validationSet.PackageId,
                validationSet.PackageNormalizedVersion,
                validationSet.ValidationTrackingId,
                validatingEntity.Status,
                aNewEntityInValidatingStateExists,
                proceed
                );
            return proceed;
        }
    }
}
