// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using NuGet.Jobs.Validation.Symbols.Core;
using NuGet.Services.ServiceBus;
using NuGet.Jobs.Validation;
using NuGet.Jobs.Validation.Storage;
using NuGet.Services.Validation;

namespace Validation.Symbols
{
    public class SymbolsValidatorMessageHandler : IMessageHandler<SymbolsValidatorMessage>
    {
        private const int MaxDBSaveRetry = 5;

        private readonly ILogger<SymbolsValidatorMessageHandler> _logger;
        private readonly ISymbolsValidatorService _symbolValidatorService;
        private readonly IValidatorStateService _validatorStateService;
        private readonly IPackageValidationEnqueuer _validationEnqueuer;
        private readonly IFeatureFlagService _featureFlagService;

        public SymbolsValidatorMessageHandler(
            ISymbolsValidatorService symbolValidatorService,
            IValidatorStateService validatorStateService,
            IPackageValidationEnqueuer validationEnqueuer,
            IFeatureFlagService featureFlagService,
            ILogger<SymbolsValidatorMessageHandler> logger)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _symbolValidatorService = symbolValidatorService ?? throw new ArgumentNullException(nameof(symbolValidatorService));
            _validatorStateService = validatorStateService ?? throw new ArgumentNullException(nameof(validatorStateService));
            _validationEnqueuer = validationEnqueuer ?? throw new ArgumentNullException(nameof(validationEnqueuer));
            _featureFlagService = featureFlagService ?? throw new ArgumentNullException(nameof(featureFlagService));
        }

        public async Task<bool> HandleAsync(SymbolsValidatorMessage message)
        {
            using (_logger.BeginScope("{ValidatorName}: Handling message for {PackageId} {PackageVersion} validation set {ValidationId}",
                    ValidatorName.SymbolsValidator,
                    message.PackageId,
                    message.PackageNormalizedVersion,
                    message.ValidationId))
            {
                if (message == null)
                {
                    throw new ArgumentNullException(nameof(message));
                }
                var validation = await _validatorStateService.GetStatusAsync(message.ValidationId);

                // A validation should be queued with ValidatorState == Incomplete.
                if (validation == null)
                {
                    _logger.LogInformation(
                        "{ValidatorName} : Could not find validation entity, requeueing (package: {PackageId} {PackageVersion}, validationId: {ValidationId})",
                        ValidatorName.SymbolsValidator,
                        message.PackageId,
                        message.PackageNormalizedVersion,
                        message.ValidationId);

                    // Message may be retried.
                    return false;
                }
                else if (validation.State == ValidationStatus.NotStarted)
                {
                    _logger.LogWarning(
                        "{ValidatorName}:Unexpected status '{ValidatorState}' when 'Incomplete' was expected, requeueing package id: {PackageId} package version: {PackageVersion} validation id: {ValidationId})",
                        ValidatorName.SymbolsValidator,
                        validation.State,
                        message.PackageId,
                        message.PackageNormalizedVersion,
                        message.ValidationId);

                    // Message may be retried.
                    return false;
                }
                // Final states
                else if (validation.State == ValidationStatus.Failed || validation.State == ValidationStatus.Succeeded)
                {
                    _logger.LogWarning(
                        "{ValidatorName}:Terminal symbol verification status '{ValidatorState}' when 'Incomplete' was expected, dropping message (package id: {PackageId} package version: {PackageVersion} validation id: {ValidationId})",
                        ValidatorName.SymbolsValidator,
                        validation.State,
                        message.PackageId,
                        message.PackageNormalizedVersion,
                        message.ValidationId);

                    // Consume the message.
                    return true;
                }

                var validationResult = await _symbolValidatorService.ValidateSymbolsAsync(message, CancellationToken.None);

                if (validationResult.Status == ValidationStatus.Failed || validationResult.Status == ValidationStatus.Succeeded)
                {
                    validation.State = validationResult.Status;
                    validation.ValidatorIssues = validation.ValidatorIssues ?? new List<ValidatorIssue>();
                    foreach (var issue in validationResult.Issues)
                    {
                        validation.ValidatorIssues.Add(new ValidatorIssue
                        {
                            IssueCode = issue.IssueCode,
                            Data = issue.Serialize(),
                        });
                    }

                    if (validationResult.NupkgUrl != null)
                    {
                        validation.NupkgUrl = validationResult.NupkgUrl;
                    }

                    var completed = await SaveStatusAsync(validation, message, MaxDBSaveRetry);
                    if (completed && _featureFlagService.IsQueueBackEnabled())
                    {
                        _logger.LogInformation("Sending queue-back message for validation {ValidationId}.", message.ValidationId);
                        var messageData = PackageValidationMessageData.NewCheckValidator(message.ValidationId);
                        await _validationEnqueuer.SendMessageAsync(messageData);
                    }

                    return completed;
                }

                _logger.LogWarning(
                                "{ValidatorName}:The validation did not return a complete status for package {PackageId} {PackageVersion} for validation id: {ValidationId} .",
                                ValidatorName.SymbolsValidator,
                                message.PackageId,
                                message.PackageNormalizedVersion,
                                message.ValidationId);
                return false;
            }
        }

        private async Task<bool> SaveStatusAsync(ValidatorStatus validation, SymbolsValidatorMessage message, int maxRetries)
        {
            bool saveStatus = false;
            int currentRetry = 0;
            while (!saveStatus && ++currentRetry < maxRetries)
            {
                try
                {
                    _logger.LogInformation(
                        "{ValidatorName}:Try to save validation status package {PackageId} {PackageVersion} for validation id: {ValidationId} RetryCount: {currentRetry}.",
                        ValidatorName.SymbolsValidator,
                        message.PackageId,
                        message.PackageNormalizedVersion,
                        message.ValidationId,
                        currentRetry);
                    saveStatus = await _validatorStateService.SaveStatusAsync(validation) == SaveStatusResult.Success;
                }
                catch (Exception e) 
                {
                    _logger.LogWarning(
                        0,
                        e,
                        "{ValidatorName}:Unable to save to save package {PackageId} {PackageVersion} for validation id: {ValidationId}.",
                        ValidatorName.SymbolsValidator,
                        message.PackageId,
                        message.PackageNormalizedVersion,
                        message.ValidationId);
                }
            }
            if(!saveStatus)
            {
                _logger.LogWarning(
                        "{ValidatorName}:Unable to save to save after {MaxRetries}. Requeueing package {PackageId} {PackageVersion} for validation id: {ValidationId}.",
                        ValidatorName.SymbolsValidator,
                        maxRetries,
                        message.PackageId,
                        message.PackageNormalizedVersion,
                        message.ValidationId);
            }
            return saveStatus;
        }
    }
}
