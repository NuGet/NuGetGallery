// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using NuGet.Jobs.Validation.PackageSigning.Messages;
using NuGet.Jobs.Validation.PackageSigning.Storage;
using NuGet.Packaging.Signing;
using NuGet.Services.Validation;

namespace NuGet.Jobs.Validation.PackageSigning.ExtractAndValidateSignature
{
    public class SignatureValidator : ISignatureValidator
    {
        private readonly IPackageSigningStateService _packageSigningStateService;
        private readonly ILogger<SignatureValidator> _logger;

        public SignatureValidator(IPackageSigningStateService packageSigningStateService, ILogger<SignatureValidator> logger)
        {
            _packageSigningStateService = packageSigningStateService ?? throw new ArgumentNullException(nameof(packageSigningStateService));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public async Task ValidateAsync(ISignedPackageReader signedPackageReader, ValidatorStatus validation, SignatureValidationMessage message, CancellationToken cancellationToken)
        {
            if (!await signedPackageReader.IsSignedAsync(cancellationToken))
            {
                await HandleUnsignedPackageAsync(validation, message);
            }
            else
            {
                await HandleSignedPackageAsync(validation, message);
            }
        }
        
        private async Task HandleUnsignedPackageAsync(ValidatorStatus validation, SignatureValidationMessage message)
        {
            _logger.LogInformation(
                "Package {PackageId} {PackageVersion} is unsigned, no additional validations necessary for {ValidationId}.",
                message.PackageId,
                message.PackageVersion,
                message.ValidationId);

            // Update the package's state.
            await _packageSigningStateService.SetPackageSigningState(
                validation.PackageKey,
                message.PackageId,
                message.PackageVersion,
                status: PackageSigningStatus.Unsigned);

            validation.State = ValidationStatus.Succeeded;
        }

        private Task HandleSignedPackageAsync(ValidatorStatus validation, SignatureValidationMessage message)
        {
            _logger.LogInformation(
                "Signed package {PackageId} {PackageVersion} is blocked for validation {ValidationId}.",
                message.PackageId,
                message.PackageVersion,
                message.ValidationId);

            // Pre-wave 1: block signed packages on nuget.org
            validation.State = ValidationStatus.Failed;

            return Task.CompletedTask;
        }
    }
}
