// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Net.Http;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using NuGet.Jobs.Validation.PackageSigning.Messages;
using NuGet.Jobs.Validation.PackageSigning.Storage;
using NuGet.Services.ServiceBus;
using NuGet.Services.Validation;
using NuGet.Versioning;

namespace NuGet.Jobs.Validation.PackageSigning.ExtractAndValidateSignature
{
    /// <summary>
    /// The handler for <see cref="SignatureValidationMessage"/>.
    /// Upon receiving a message, this will extract all metadata (including certificates) from a nupkg, 
    /// and verify the <see cref="PackageSignature"/> using extracted metadata. 
    /// This doesn't do online revocation checks.
    /// </summary>
    public class SignatureValidationMessageHandler
        : IMessageHandler<SignatureValidationMessage>
    {
        private readonly IValidatorStateService _validatorStateService;
        private readonly IPackageSigningStateService _packageSigningStateService;
        private readonly ILogger<SignatureValidationMessageHandler> _logger;

        /// <summary>
        /// Instantiate's a new package signatures validator.
        /// </summary>
        /// <param name="validationContext">The persisted validation context.</param>
        /// <param name="certificateStore">The persisted certificate store.</param>
        /// <param name="validatorStateService">The service used to retrieve and persist this validator's state.</param>
        /// <param name="packageSigningStateService">The service used to retrieve and persist package signing state.</param>
        public SignatureValidationMessageHandler(
            IValidatorStateService validatorStateService,
            IPackageSigningStateService packageSigningStateService,
            ILogger<SignatureValidationMessageHandler> logger)
        {
            _validatorStateService = validatorStateService ?? throw new ArgumentNullException(nameof(validatorStateService));
            _packageSigningStateService = packageSigningStateService ?? throw new ArgumentNullException(nameof(packageSigningStateService));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        /// <summary>
        /// Extract the package metadata and verify signature.
        /// </summary>
        /// <param name="message">The message requesting the signature verification.</param>
        /// <returns>
        /// Returns <c>true</c> if the validation completed; otherwise <c>false</c>.
        /// If <c>false</c>, the validation should be retried later.
        /// </returns>
        public async Task<bool> HandleAsync(SignatureValidationMessage message)
        {
            // Find the signature validation entity that matches this message.
            var validation = await _validatorStateService.GetStatusAsync(message.ValidationId);

            // A signature validation should be queued with ValidatorState == Incomplete.
            if (validation == null)
            {
                _logger.LogInformation(
                    "Could not find validation entity, requeueing (package: {PackageId} {PackageVersion}, validationId: {ValidationId})",
                    message.PackageId,
                    message.PackageVersion,
                    message.ValidationId);

                // Message may be retried.
                return false;
            }
            else if (validation.State != ValidationStatus.Incomplete)
            {
                _logger.LogWarning(
                    "Invalid signature verification status '{ValidatorState}' when 'Incomplete' was expected, dropping message (package id: {PackageId} package version: {PackageVersion} validation id: {ValidationId})",
                    validation.State,
                    message.PackageId,
                    message.PackageVersion,
                    message.ValidationId);

                // Consume the message.
                return true;
            }

            // Validate package
            // TODO: consume actual client nupkg's containing missing signing APIs
            if (!IsSigned(message.PackageVersion))
            {
                return await HandleUnsignedPackageAsync(validation, message);
            }
            else
            {
                // Pre-wave 1: block signed packages on nuget.org
                return await BlockSignedPackageAsync(validation, message);
            }
        }

        private bool IsSigned(string packageVersion)
        {
            var nugetVersion = NuGetVersion.Parse(packageVersion);
            return nugetVersion.IsPrerelease && string.Equals(nugetVersion.Release, "signed", StringComparison.OrdinalIgnoreCase);
        }

        private async Task<bool> HandleUnsignedPackageAsync(ValidatorStatus validation, SignatureValidationMessage message)
        {
            _logger.LogInformation(
                        "Package {PackageId} {PackageVersion} is unsigned, no additional validations necessary.",
                        message.PackageId,
                        message.PackageVersion);

            var savePackageSigningStateResult = await _packageSigningStateService.TrySetPackageSigningState(
                validation.PackageKey,
                message.PackageId,
                message.PackageVersion,
                isRevalidationRequest: false,
                status: PackageSigningStatus.Unsigned);

            validation.State = ValidationStatus.Succeeded;
            var saveStateResult = await _validatorStateService.SaveStatusAsync(validation);

            // Consume the message if successfully saved state.
            return saveStateResult == SaveStatusResult.Success;
        }

        private async Task<bool> BlockSignedPackageAsync(ValidatorStatus validation, SignatureValidationMessage message)
        {
            _logger.LogInformation(
                        "Signed package {PackageId} {PackageVersion} is blocked.",
                        message.PackageId,
                        message.PackageVersion);

            validation.State = ValidationStatus.Failed;
            var saveStateResult = await _validatorStateService.SaveStatusAsync(validation);

            // Consume the message if successfully saved state.
            return saveStateResult == SaveStatusResult.Success;
        }
    }
}
