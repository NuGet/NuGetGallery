// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Data.Entity.Infrastructure;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using NuGet.Jobs.Validation.PackageSigning.Messages;
using NuGet.Jobs.Validation.Storage;
using NuGet.Services.ServiceBus;
using NuGet.Services.Validation;

namespace NuGet.Jobs.Validation.PackageSigning.ProcessSignature
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
        private readonly IFileDownloader _packageDownloader;
        private readonly IValidatorStateService _validatorStateService;
        private readonly ISignatureValidator _signatureValidator;
        private readonly IPackageValidationEnqueuer _validationEnqueuer;
        private readonly IFeatureFlagService _featureFlagService;
        private readonly ILogger<SignatureValidationMessageHandler> _logger;

        /// <summary>
        /// Instantiate's a new package signatures validator.
        /// </summary>
        /// <param name="httpClient">The HTTP client used to download packages.</param>
        /// <param name="validatorStateService">The service used to retrieve and persist this validator's state.</param>
        /// <param name="packageSigningStateService">The service used to retrieve and persist package signing state.</param>
        /// <param name="logger">The logger that should be used.</param>
        public SignatureValidationMessageHandler(
            IFileDownloader packageDownloader,
            IValidatorStateService validatorStateService,
            ISignatureValidator signatureValidator,
            IPackageValidationEnqueuer validationEnqueuer,
            IFeatureFlagService featureFlagService,
            ILogger<SignatureValidationMessageHandler> logger)
        {
            _packageDownloader = packageDownloader ?? throw new ArgumentNullException(nameof(packageDownloader));
            _validatorStateService = validatorStateService ?? throw new ArgumentNullException(nameof(validatorStateService));
            _signatureValidator = signatureValidator ?? throw new ArgumentNullException(nameof(signatureValidator));
            _validationEnqueuer = validationEnqueuer ?? throw new ArgumentNullException(nameof(validationEnqueuer));
            _featureFlagService = featureFlagService ?? throw new ArgumentNullException(nameof(featureFlagService));
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
            return await HandleAsync(message, CancellationToken.None);
        }

        private async Task<bool> HandleAsync(SignatureValidationMessage message, CancellationToken cancellationToken)
        {
            using (_logger.BeginScope("Handling signature validation message for package {PackageId} {PackageVersion}, validation {ValidationId}",
                message.PackageId,
                message.PackageVersion,
                message.ValidationId))
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
                else if (validation.State == ValidationStatus.NotStarted)
                {
                    _logger.LogWarning(
                        "Unexpected signature verification status '{ValidatorState}' when 'Incomplete' was expected, requeueing (package id: {PackageId} package version: {PackageVersion} validation id: {ValidationId})",
                        validation.State,
                        message.PackageId,
                        message.PackageVersion,
                        message.ValidationId);

                    // Message may be retried.
                    return false;
                }
                else if (validation.State != ValidationStatus.Incomplete)
                {
                    _logger.LogWarning(
                        "Terminal signature verification status '{ValidatorState}' when 'Incomplete' was expected, dropping message (package id: {PackageId} package version: {PackageVersion} validation id: {ValidationId})",
                        validation.State,
                        message.PackageId,
                        message.PackageVersion,
                        message.ValidationId);

                    // Consume the message.
                    return true;
                }

                // Validate package
                using (var packageStream = await _packageDownloader.DownloadAsync(message.NupkgUri, cancellationToken))
                {
                    var result = await _signatureValidator.ValidateAsync(
                        validation.PackageKey,
                        packageStream,
                        message,
                        cancellationToken);

                    validation.State = result.State;

                    // Save any issues if the resulting state is terminal.
                    if (validation.State == ValidationStatus.Failed
                        || validation.State == ValidationStatus.Succeeded)
                    {
                        validation.ValidatorIssues = validation.ValidatorIssues ?? new List<ValidatorIssue>();
                        foreach (var issue in result.Issues)
                        {
                            validation.ValidatorIssues.Add(new ValidatorIssue
                            {
                                IssueCode = issue.IssueCode,
                                Data = issue.Serialize(),
                            });
                        }
                    }

                    // Save the .nupkg URL if the resulting state is successful.
                    if (validation.State == ValidationStatus.Succeeded
                        && result.NupkgUri != null)
                    {
                        validation.NupkgUrl = result.NupkgUri.AbsoluteUri;
                    }
                }

                // The signature validator should do all of the work to bring this validation to its completion.
                if (validation.State != ValidationStatus.Succeeded
                    && validation.State != ValidationStatus.Failed)
                {
                    _logger.LogError("The signature validator should have set the status 'Succeeded' or 'Failed', not " +
                        "'{ValidatorState}' (package id: {PackageId} package version: {PackageVersion} validation id: {ValidationId})",
                        validation.State,
                        message.PackageId,
                        message.PackageVersion,
                        message.ValidationId);

                    return false;
                }

                // Save the resulting validation status.
                var completed = await SaveStatusAsync(validation, message);
                if (completed && _featureFlagService.IsQueueBackEnabled())
                {
                    // The validation has completed (either a terminal success or a terminal failure). This message
                    // we are enqueueing notifies the orchestrator that this validator's work is done and means the
                    // orchestrator can continue with the rest of the validation process.
                    _logger.LogInformation("Sending queue-back message for validation {ValidationId}.", message.ValidationId);
                    var messageData = PackageValidationMessageData.NewCheckValidator(message.ValidationId);
                    await _validationEnqueuer.StartValidationAsync(messageData);
                }

                return completed;
            }
        }

        private async Task<bool> SaveStatusAsync(ValidatorStatus validation, SignatureValidationMessage message)
        {
            try
            {
                var saveStatus = await _validatorStateService.SaveStatusAsync(validation);

                if (saveStatus == SaveStatusResult.Success)
                {
                    // Consume the message.
                    return true;
                }
                else
                {
                    _logger.LogWarning(
                        "Unable to save to save due to stale context, requeueing package {PackageId} {PackageVersion} for validation id: {ValidationId}.",
                        message.PackageId,
                        message.PackageVersion,
                        message.ValidationId);
                }
            }
            catch (DbUpdateException e) when (e.IsUniqueConstraintViolationException())
            {
                _logger.LogWarning(
                    0,
                    e,
                    "Unable to save to save due to unique contrainst violation, requeueing package {PackageId} {PackageVersion} for validation id: {ValidationId}.",
                    message.PackageId,
                    message.PackageVersion,
                    message.ValidationId);
            }

            // Message may be retried.
            return false;
        }
    }
}
