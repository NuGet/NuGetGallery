// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Data.Entity.Infrastructure;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using NuGet.Services.Validation;
using NuGet.Versioning;

namespace NuGet.Jobs.Validation.PackageSigning.Storage
{
    public class PackageSigningStateService
        : IPackageSigningStateService
    {
        private readonly IValidationEntitiesContext _validationContext;
        private readonly ILogger<PackageSigningStateService> _logger;

        public PackageSigningStateService(
            IValidationEntitiesContext validationContext,
            ILogger<PackageSigningStateService> logger)
        {
            _validationContext = validationContext ?? throw new ArgumentNullException(nameof(validationContext));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public async Task<SavePackageSigningStateResult> TrySetPackageSigningState(
            int packageKey,
            string packageId,
            string packageVersion,
            bool isRevalidationRequest,
            PackageSigningStatus status)
        {
            if (string.IsNullOrEmpty(packageId))
            {
                throw new ArgumentException(nameof(packageId));
            }
            if (string.IsNullOrEmpty(packageVersion))
            {
                throw new ArgumentException(nameof(packageVersion));
            }

            // Check for revalidation
            var currentState = _validationContext.PackageSigningStates.FirstOrDefault(s => s.PackageKey == packageKey);
            if (isRevalidationRequest && currentState != null)
            {
                // Update existing record
                currentState.SigningStatus = status;
            }
            else
            {
                // Insert new record
                currentState = new PackageSigningState
                {
                    PackageId = packageId,
                    PackageKey = packageKey,
                    PackageNormalizedVersion = NuGetVersion.Parse(packageVersion).ToNormalizedString(),
                    SigningStatus = status
                };

                _validationContext.PackageSigningStates.Add(currentState);
            }

            try
            {
                await _validationContext.SaveChangesAsync();

                return SavePackageSigningStateResult.Success;
            }
            catch (DbUpdateException e) when (e.IsUniqueConstraintViolationException())
            {
                return SavePackageSigningStateResult.StatusAlreadyExists;
            }
            catch (DbUpdateConcurrencyException e)
            {
                _logger.LogWarning(
                    0,
                    e,
                    "Failed to update package signing state for package id {PackageId} version {PackageVersion} to status {NewStatus} due to concurrency exception.",
                    packageId,
                    packageVersion,
                    status);

                return SavePackageSigningStateResult.Stale;
            }
        }
    }
}
