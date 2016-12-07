// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using Microsoft.WindowsAzure.Storage;
using NuGet.Jobs.Validation.Common;
using NuGet.Jobs.Validation.Common.OData;
using NuGet.Jobs.Validation.Common.Validators;
using NuGet.Jobs.Validation.Common.Validators.Unzip;
using NuGet.Jobs.Validation.Common.Validators.Vcs;

namespace NuGet.Jobs.Validation.Runner
{
    public class Job
        : JobBase
    {
        private readonly List<IValidator> _validators = new List<IValidator>();

        private string _galleryBaseAddress;
        private CloudStorageAccount _cloudStorageAccount;
        private string _containerName;
        private string[] _runValidationTasks;
        private string[] _requestValidationTasks;

        public override bool Init(IDictionary<string, string> jobArgsDictionary)
        {
            try
            {
                // Configure job
                _galleryBaseAddress = JobConfigurationManager.GetArgument(jobArgsDictionary, JobArgumentNames.GalleryBaseAddress);

                var storageConnectionString = JobConfigurationManager.GetArgument(jobArgsDictionary, JobArgumentNames.DataStorageAccount);
                _cloudStorageAccount = CreateCloudStorageAccount(JobArgumentNames.DataStorageAccount, storageConnectionString);

                _containerName = JobConfigurationManager.GetArgument(jobArgsDictionary, JobArgumentNames.ContainerName);

                _runValidationTasks = JobConfigurationManager.GetArgument(jobArgsDictionary, JobArgumentNames.RunValidationTasks).Split(';');
                _requestValidationTasks = JobConfigurationManager.GetArgument(jobArgsDictionary, JobArgumentNames.RequestValidationTasks).Split(';');

                // Add validators
                if (_runValidationTasks.Contains(UnzipValidator.ValidatorName))
                {
                    _validators.Add(new UnzipValidator());
                }
                if (_runValidationTasks.Contains(VcsValidator.ValidatorName))
                {
                    _validators.Add(new VcsValidator(
                        JobConfigurationManager.GetArgument(jobArgsDictionary, JobArgumentNames.VcsValidatorServiceUrl),
                        JobConfigurationManager.GetArgument(jobArgsDictionary, JobArgumentNames.VcsValidatorCallbackUrl),
                        JobConfigurationManager.GetArgument(jobArgsDictionary, JobArgumentNames.VcsValidatorAlias),
                        JobConfigurationManager.GetArgument(jobArgsDictionary, JobArgumentNames.VcsPackageUrlTemplate)));
                }

                return true;
            }
            catch (Exception ex)
            {
                Trace.TraceError(ex.ToString());
            }

            return false;
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

        public override async Task<bool> Run()
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

            return true;
        }

        private async Task RunValidationsAsync(IValidator validator)
        {
            Trace.TraceInformation("Start running RunValidationsAsync for validator {0}...", validator.Name);

            // Services
            var packageValidationTable = new PackageValidationTable(_cloudStorageAccount, _containerName);
            var packageValidationAuditor = new PackageValidationAuditor(_cloudStorageAccount, _containerName);
            var packageValidationQueue = new PackageValidationQueue(_cloudStorageAccount, _containerName);
            var notificationService = new NotificationService(_cloudStorageAccount, _containerName);

            // Get messages to process
            var messages = await packageValidationQueue.DequeueAsync(validator.Name, 16, validator.VisibilityTimeout);
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
                        Message = $"Message has been attempted too many times and is being deadlettered. Aborting validator."
                    });
                }

                if (validationResult != ValidationResult.Deadlettered)
                {
                    try
                    {
                        // Perform the validation
                        Trace.TraceInformation("Start running validator {0} for validation {1} - package {2} {3}...", validator.Name, message.ValidationId, message.PackageId, message.PackageVersion);
                        validationResult = await validator.ValidateAsync(message, auditEntries);
                        Trace.TraceInformation("Finished running validator {0} for validation {1} - package {2} {3}. Result: {4}", validator.Name, message.ValidationId, message.PackageId, message.PackageVersion, validationResult);
                    }
                    catch (Exception ex)
                    {
                        // Audit the exception, but do not remove the message yet.
                        // We want to retry validation on next run.
                        auditEntries.Add(new PackageValidationAuditEntry
                        {
                            Timestamp = DateTimeOffset.UtcNow,
                            ValidatorName = validator.Name,
                            Message = $"Exception thrown during validation - {ex.Message}\r\n{ex.StackTrace}"
                        });

                        Trace.TraceInformation("Exception while running validator {0} for validation {1} - package {2} {3}. {4} {5}", validator.Name, message.ValidationId, message.PackageId, message.PackageVersion, ex.Message, ex.StackTrace);
                    }
                }

                // Process message
                if (validationResult != ValidationResult.Unknown)
                {
                    // Update our tracking entity
                    var packageValidationEntity = await packageValidationTable.GetValidationAsync(message.ValidationId);
                    if (packageValidationEntity != null)
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

            Trace.TraceInformation("Finished running RunValidationsAsync for validator {0}.", validator.Name);
        }

        private async Task RunOrchestrateAsync()
        {
            // Retrieve cursor (last created / last edited)
            var cursor = new PackageValidationOrchestrationCursor(_cloudStorageAccount, _containerName + "-audit", "cursor.json");
            await cursor.LoadAsync();
            
            // Setup package validation service
            var packageValidationService = new PackageValidationService(_cloudStorageAccount, _containerName);

            // Get reference timestamps
            var referenceLastCreated = cursor.LastCreated ?? DateTimeOffset.UtcNow.AddMinutes(-15);
            var referenceLastEdited = cursor.LastEdited ?? DateTimeOffset.UtcNow.AddMinutes(-15);

            // Fetch newly added / edited packages and enqueue validations
            using (var client = new HttpClient())
            {
                var packages = new HashSet<NuGetPackage>(new NuGetV2PackageEqualityComparer());

                var feed = new NuGetV2Feed(client);

                var createdPackagesUrl = MakePackageQueryUrl(_galleryBaseAddress, "Created", referenceLastCreated);
                var createdPackages = await feed.GetPackagesAsync(createdPackagesUrl, continuationsToFollow: 0);
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
    }
}