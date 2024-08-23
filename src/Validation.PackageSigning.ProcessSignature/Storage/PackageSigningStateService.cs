// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Data.Entity;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using NuGet.Services.Validation;
using NuGet.Versioning;

namespace NuGet.Jobs.Validation.PackageSigning.Storage
{
    public class PackageSigningStateService : IPackageSigningStateService
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

        public async Task SetPackageSigningState(
            int packageKey,
            string packageId,
            string packageVersion,
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

            // Update the signing state if it already exists, otherwise, create a new record.
            var signatureState = await _validationContext
                .PackageSigningStates
                .Include(s => s.PackageSignatures)
                .FirstOrDefaultAsync(s => s.PackageKey == packageKey);

            if (signatureState != null)
            {
                signatureState.SigningStatus = status;

                // Remove all stored signatures if the package is transitioning to an unsigned state.
                if (status == PackageSigningStatus.Unsigned)
                {
                    foreach (var signature in signatureState.PackageSignatures.ToList())
                    {
                        _validationContext.PackageSignatures.Remove(signature);
                    }
                }
            }
            else
            {
                _validationContext.PackageSigningStates.Add(new PackageSigningState
                {
                    PackageId = packageId,
                    PackageKey = packageKey,
                    PackageNormalizedVersion = NuGetVersion.Parse(packageVersion).ToNormalizedString(),
                    SigningStatus = status
                });
            }
        }

        public async Task<bool> HasPackageSigningStateAsync(int packageKey)
        {
            return await _validationContext.PackageSigningStates
                .Where(s => s.PackageKey == packageKey)
                .AnyAsync();
        }
    }
}
