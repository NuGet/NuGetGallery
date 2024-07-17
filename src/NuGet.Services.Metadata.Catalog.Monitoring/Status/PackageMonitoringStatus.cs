// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.WindowsAzure.Storage;
using Newtonsoft.Json;
using NuGet.Services.Metadata.Catalog.Helpers;
using NuGetGallery;

namespace NuGet.Services.Metadata.Catalog.Monitoring
{
    /// <summary>
    /// The validation status of a package. Contains whether or not validation ran, and if the validation succeeded.
    /// </summary>
    public class PackageMonitoringStatus
    {
        /// <summary>
        /// The identity of the package.
        /// </summary>
        [JsonProperty("package")]
        public FeedPackageIdentity Package { get; }

        [JsonProperty("state")]
        public PackageState State
        {
            get
            {
                if (ValidationException != null || HasResultsOfType(TestResult.Fail))
                {
                    return PackageState.Invalid;
                }

                if (HasResultsOfType(TestResult.Pending))
                {
                    return PackageState.Unknown;
                }

                return PackageState.Valid;
            }
        }

        /// <summary>
        /// If validation ran, the results of the validation.
        /// </summary>
        [JsonProperty("validationResult")]
        public PackageValidationResult ValidationResult { get; }

        /// <summary>
        /// If validation failed to run, the exception that was thrown.
        /// </summary>
        [JsonProperty("validationException")]
        public Exception ValidationException { get; }

        /// <summary>
        /// If this status was loaded from storage, this access condition should be used to overwrite it.
        /// </summary>
        [JsonIgnore]
        public IAccessCondition AccessCondition { get; set; }

        /// <summary>
        /// When this status is saved to storage, the save operation should use these <see cref="AccessCondition"/>s to save to the containers associated with each <see cref="PackageState"/>.
        /// </summary>
        [JsonIgnore]
        public IDictionary<PackageState, IAccessCondition> ExistingState { get; }

        [JsonConstructor]
        public PackageMonitoringStatus(FeedPackageIdentity package, PackageValidationResult validationResult, Exception validationException)
            : this()
        {
            Package = package;
            ValidationResult = validationResult;
            ValidationException = validationException;
        }

        public PackageMonitoringStatus(PackageValidationResult result)
            : this()
        {
            ValidationResult = result ?? throw new ArgumentNullException(nameof(result));
            Package = new FeedPackageIdentity(result.Package);
        }

        public PackageMonitoringStatus(FeedPackageIdentity package, Exception exception)
            : this()
        {
            Package = package ?? throw new ArgumentNullException(nameof(package));
            ValidationException = exception ?? throw new ArgumentNullException(nameof(exception));
        }

        private PackageMonitoringStatus()
        {
            AccessCondition = PackageMonitoringStatusAccessConditionHelper.FromUnknown();
            ExistingState = new Dictionary<PackageState, IAccessCondition>();
            foreach (var state in Enum.GetValues(typeof(PackageState)).Cast<PackageState>())
            {
                ExistingState[state] = PackageMonitoringStatusAccessConditionHelper.FromUnknown();
            }
        }

        private bool HasResultsOfType(TestResult result)
        {
            return ValidationResult.AggregateValidationResults.Any(
                r => r.ValidationResults.Any(
                    v => v.Result == result));
        }
    }
}
