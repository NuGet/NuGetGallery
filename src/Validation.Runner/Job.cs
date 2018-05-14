// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.ComponentModel.Design;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.WindowsAzure.Storage;
using NuGet.Jobs.Validation.Common;
using NuGet.Jobs.Validation.Common.OData;
using NuGet.Jobs.Validation.Common.Validators;
using NuGet.Jobs.Validation.Common.Validators.Vcs;
using NuGet.Services.VirusScanning.Vcs;

namespace NuGet.Jobs.Validation.Runner
{
    public class Job
        : JobBase
    {
        private const int DefaultBatchSize = 10;
        private readonly List<IValidator> _validators = new List<IValidator>();

        private string _galleryBaseAddress;
        private CloudStorageAccount _cloudStorageAccount;
        private string _containerName;
        private string[] _runValidationTasks;
        private string[] _requestValidationTasks;
        private int _batchSize;

        public override void Init(IServiceContainer serviceContainer, IDictionary<string, string> jobArgsDictionary)
        {
            _galleryBaseAddress = JobConfigurationManager.GetArgument(jobArgsDictionary, JobArgumentNames.GalleryBaseAddress);

            var storageConnectionString = JobConfigurationManager.GetArgument(jobArgsDictionary, JobArgumentNames.DataStorageAccount);
            _cloudStorageAccount = CreateCloudStorageAccount(JobArgumentNames.DataStorageAccount, storageConnectionString);

            _containerName = JobConfigurationManager.GetArgument(jobArgsDictionary, JobArgumentNames.ContainerName);

            _runValidationTasks = JobConfigurationManager.GetArgument(jobArgsDictionary, JobArgumentNames.RunValidationTasks).Split(';');
            _requestValidationTasks = JobConfigurationManager.GetArgument(jobArgsDictionary, JobArgumentNames.RequestValidationTasks).Split(';');
            _batchSize = JobConfigurationManager.TryGetIntArgument(jobArgsDictionary, JobArgumentNames.BatchSize) ?? DefaultBatchSize;

            // Add validators
            if (_runValidationTasks.Contains(VcsValidator.ValidatorName))
            {
                var serviceUrl = new Uri(JobConfigurationManager.GetArgument(jobArgsDictionary, JobArgumentNames.VcsValidatorServiceUrl));
                var consumerCode = "DIRECT";
                var callbackUrl = new Uri(JobConfigurationManager.GetArgument(jobArgsDictionary, JobArgumentNames.VcsValidatorCallbackUrl));
                var packageUrlTemplate = JobConfigurationManager.GetArgument(jobArgsDictionary, JobArgumentNames.PackageUrlTemplate);
                var submitterAlias = JobConfigurationManager.GetArgument(jobArgsDictionary, JobArgumentNames.VcsValidatorSubmitterAlias);

                // if contact alias set, use it, if not, use submitter alias.
                var contactAlias = JobConfigurationManager.TryGetArgument(jobArgsDictionary, JobArgumentNames.VcsContactAlias) ?? submitterAlias;
                
                var scanningService = new VcsVirusScanningService(
                    serviceUrl,
                    consumerCode,
                    contactAlias,
                    submitterAlias,
                    LoggerFactory);

                _validators.Add(new VcsValidator(
                    callbackUrl,
                    packageUrlTemplate,
                    scanningService,
                    LoggerFactory.CreateLogger<VcsValidator>()));
            }
        }

        private static CloudStorageAccount CreateCloudStorageAccount(string argumentName, string storageConnectionString)
        {
            if (string.IsNullOrEmpty(storageConnectionString))
            {
                throw new ArgumentException("Job parameter " + argumentName + " for Azure Cloud Storage Account is not defined.");
            }

            CloudStorageAccount account;
            if (CloudStorageAccount.TryParse(storageConnectionString, out account))
            {
                return account;
            }

            throw new ArgumentException("Job parameter " + argumentName + " for Azure Cloud Storage Account is invalid.");
        }

        public override async Task Run()
        {
            if (!_runValidationTasks.Any())
            {
                throw new ArgumentException("Job parameter " + JobArgumentNames.RunValidationTasks + " must be specified.");
            }

            if (!_requestValidationTasks.Any())
            {
                throw new ArgumentException("Job parameter " + JobArgumentNames.RequestValidationTasks + " must be specified.");
            }

            // Run any of the subcommands specified in the "RunValidationTasks" argument
            foreach (var validationTask in _runValidationTasks)
            {
                if (validationTask.StartsWith("validator", StringComparison.OrdinalIgnoreCase))
                {
                    var validator = _validators.FirstOrDefault(
                        v => String.Equals(v.Name, validationTask, StringComparison.OrdinalIgnoreCase));

                    if (validator != null)
                    {
                        await RunValidationsAsync(validator);
                    }
                }
                else if (string.Equals(validationTask, "orchestrate", StringComparison.OrdinalIgnoreCase))
                {
                    await RunOrchestrateAsync();
                }
            }
        }

        private async Task RunValidationsAsync(IValidator validator)
        {
            Logger.LogInformation($"{{{TraceConstant.EventName}}}: " +
                    $"Checking the queue of {{{TraceConstant.ValidatorName}}}",
                "ValidatorQueueCheck",
                validator.Name);

            // Services
            var packageValidationTable = new PackageValidationTable(_cloudStorageAccount, _containerName);
            var packageValidationAuditor = new PackageValidationAuditor(_cloudStorageAccount, _containerName, LoggerFactory);
            var packageValidationQueue = new PackageValidationQueue(_cloudStorageAccount, _containerName, LoggerFactory);
            var notificationService = new NotificationService(_cloudStorageAccount, _containerName);

            // Get messages to process
            var messages = await packageValidationQueue.DequeueAsync(validator.Name, _batchSize, validator.VisibilityTimeout);
            foreach (var message in messages)
            {
                // Audit entry collection to which our validator can write
                var auditEntries = new List<PackageValidationAuditEntry>();
                var validationResult = ValidationResult.Unknown;

                // Deadlettering
                if (message.DequeueCount > 10)
                {
                    validationResult = ValidationResult.Deadlettered;

                    auditEntries.Add(new PackageValidationAuditEntry
                    {
                        Timestamp = DateTimeOffset.UtcNow,
                        ValidatorName = validator.Name,
                        Message = $"Message has been attempted too many times and is being deadlettered. Aborting validator.",
                        EventId = ValidationEvent.Deadlettered,
                    });
                }

                if (validationResult != ValidationResult.Deadlettered)
                {
                    try
                    {
                        // Perform the validation
                        Logger.LogInformation($"Starting validator {{{TraceConstant.ValidatorName}}} " +
                                $"for validation {{{TraceConstant.ValidationId}}} " +
                                $"- package {{{TraceConstant.PackageId}}} " +
                                $"v. {{{TraceConstant.PackageVersion}}}...", 
                            validator.Name, 
                            message.ValidationId, 
                            message.PackageId, 
                            message.PackageVersion);

                        validationResult = await validator.ValidateAsync(message, auditEntries);

                        Logger.LogInformation($"Finished running validator {{{TraceConstant.ValidatorName}}} " +
                                $"for validation {{{TraceConstant.ValidationId}}} " +
                                $"- package {{{TraceConstant.PackageId}}} " +
                                $"v. {{{TraceConstant.PackageVersion}}}. " +
                                $"Result: {{{TraceConstant.ValidationResult}}}", 
                            validator.Name, 
                            message.ValidationId, 
                            message.PackageId, 
                            message.PackageVersion, 
                            validationResult);
                    }
                    catch (Exception ex)
                    {
                        // Audit the exception, but do not remove the message yet.
                        // We want to retry validation on next run.
                        auditEntries.Add(new PackageValidationAuditEntry
                        {
                            Timestamp = DateTimeOffset.UtcNow,
                            ValidatorName = validator.Name,
                            Message = $"Exception thrown during validation - {ex.Message}\r\n{ex.StackTrace}",
                            EventId = ValidationEvent.ValidatorException,
                        });

                        Logger.LogError(TraceEvent.ValidatorException, ex, 
                                $"Exception while running validator {{{TraceConstant.ValidatorName}}} " +
                                $"for validation {{{TraceConstant.ValidationId}}} " +
                                $"- package {{{TraceConstant.PackageId}}} " +
                                $"v. {{{TraceConstant.PackageVersion}}}",
                            validator.Name,
                            message.ValidationId, 
                            message.PackageId, 
                            message.PackageVersion);
                    }
                }

                // Process message
                if (validationResult != ValidationResult.Unknown)
                {
                    TrackValidatorResult(validator.Name, message.ValidationId, validationResult.ToString(), message.PackageId, message.PackageVersion);

                    // Update our tracking entity
                    var packageValidationEntity = await packageValidationTable.GetValidationAsync(message.ValidationId);
                    if (packageValidationEntity != null && validationResult != ValidationResult.Asynchronous)
                    {
                        packageValidationEntity.ValidatorCompleted(validator.Name, validationResult);
                        await packageValidationTable.StoreAsync(packageValidationEntity);
                    }

                    // Remove the message
                    await packageValidationQueue.DeleteAsync(validator.Name, message);
                }

                // Write audit entries
                await packageValidationAuditor.WriteAuditEntriesAsync(message.ValidationId, message.PackageId, message.PackageVersion, auditEntries);

                // Process failure
                if (validationResult == ValidationResult.Failed || validationResult == ValidationResult.Deadlettered)
                {
                    var audit = await packageValidationAuditor.ReadAuditAsync(message.ValidationId, message.PackageId, message.PackageVersion);

                    await notificationService.SendNotificationAsync(
                        "validation",
                        $"Validation {message.ValidationId} ({message.PackageId} {message.PackageVersion}) returned '{validationResult}'",
                        audit.Humanize());
                }
            }

            Logger.LogInformation($"Done checking the queue of {{{TraceConstant.ValidatorName}}}", validator.Name);
        }

        private async Task RunOrchestrateAsync()
        {
            Logger.LogInformation($"{{{TraceConstant.EventName}}}: Attempting orchestration",
                "OrchestrationAttempt");

            // Retrieve cursor (last created / last edited)
            var cursor = new PackageValidationOrchestrationCursor(_cloudStorageAccount, _containerName + "-audit", "cursor.json", LoggerFactory);
            await cursor.LoadAsync();
            
            // Setup package validation service
            var packageValidationService = new PackageValidationService(_cloudStorageAccount, _containerName, LoggerFactory);

            // Get reference timestamps
            var referenceLastCreated = cursor.LastCreated ?? DateTimeOffset.UtcNow.AddMinutes(-15);
            var referenceLastEdited = cursor.LastEdited ?? DateTimeOffset.UtcNow.AddMinutes(-15);

            // Fetch newly added / edited packages and enqueue validations
            using (var client = new HttpClient())
            {
                var packages = new HashSet<NuGetPackage>(new NuGetV2PackageEqualityComparer());

                var feed = new NuGetV2Feed(client, LoggerFactory.CreateLogger<NuGetV2Feed>());

                var createdPackagesUrl = MakePackageQueryUrl(_galleryBaseAddress, "Created", referenceLastCreated);
                Logger.LogInformation("Querying packages created since {StartTime}, URL: {QueryUrl}", referenceLastCreated, createdPackagesUrl);
                var createdPackages = await feed.GetPackagesAsync(
                    createdPackagesUrl,
                    includeDownloadUrl: false,
                    continuationsToFollow: 0);

                foreach (var package in createdPackages)
                {
                    packages.Add(package);

                    if (package.Created > cursor.LastCreated || cursor.LastCreated == null)
                    {
                        cursor.LastCreated = package.Created;
                    }
                }

                // todo: do we also want to check edited packages again?
                //var editedPackagesUrl = MakePackageQueryUrl(_galleryBaseAddress, "LastEdited", referenceLastEdited);
                //var editedPackages = await feed.GetPackagesAsync(editedPackagesUrl, followContinuations: true);
                //foreach (var package in editedPackages)
                //{
                //    //packages.Add(package);

                //    if (package.LastEdited > cursor.LastEdited || cursor.LastEdited == null)
                //    {
                //        cursor.LastEdited = package.LastEdited;
                //    }
                //}

                // Start the validation process for each package
                foreach (var package in packages)
                {
                    await packageValidationService.StartValidationProcessAsync(package, _requestValidationTasks);
                }

                // Store cursor
                await cursor.SaveAsync();
            }

            // TODO: check for validations that have never been executed?
        }

        private static Uri MakePackageQueryUrl(string source, string propertyName, DateTimeOffset since)
        {
            var address = string.Format("{0}/Packages?$filter={1} gt DateTime'{2}'&$orderby={1}",
                source.Trim('/'),
                propertyName,
                since.UtcDateTime.ToString("O"));

            return new Uri(address);
        }

        /// <summary>
        /// Tracks the result of validation. If result is <see cref="ValidationResult.Asynchronous"/> then tracks it in 
        /// a separate event (since it is non-terminal result, want to make it trivially distinguishable from terminal).
        /// </summary>
        /// <param name="validatorName">The name of the validator</param>
        /// <param name="validationId">Validation ID of the finished validator</param>
        /// <param name="result">String representation of the outcome</param>
        /// <param name="packageId">Package ID</param>
        /// <param name="packageVersion">Package version</param>
        private void TrackValidatorResult(string validatorName, Guid validationId, string result, string packageId, string packageVersion)
        {
            if (result == ValidationResult.Asynchronous.ToString())
            {
                Logger.LogInformation($"{{{TraceConstant.EventName}}}: " +
                        $"running a {{{TraceConstant.ValidatorName}}} " +
                        $"ValidationID: {{{TraceConstant.ValidationId}}} " +
                        $"for package {{{TraceConstant.PackageId}}} " +
                        $"v.{{{TraceConstant.PackageVersion}}} resulted in starting async task",
                    "ValidatorAsync",
                    validatorName,
                    validationId,
                    packageId,
                    packageVersion);
            }
            else
            {
                Logger.TrackValidatorResult(validatorName, validationId, result, packageId, packageVersion);
            }
        }
    }
}