// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Linq;
using Newtonsoft.Json;
using NuGet.Services.Metadata.Catalog.Helpers;

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
        
        [JsonConstructor]
        public PackageMonitoringStatus(FeedPackageIdentity package, PackageValidationResult validationResult, Exception validationException)
        {
            Package = package;
            ValidationResult = validationResult;
            ValidationException = validationException;
        }

        public PackageMonitoringStatus(PackageValidationResult result)
        {
            ValidationResult = result ?? throw new ArgumentNullException(nameof(result));
            Package = new FeedPackageIdentity(result.Package);
        }

        public PackageMonitoringStatus(FeedPackageIdentity package, Exception exception)
        {
            Package = package ?? throw new ArgumentNullException(nameof(package));
            ValidationException = exception ?? throw new ArgumentNullException(nameof(exception));
        }

        private bool HasResultsOfType(TestResult result)
        {
            return ValidationResult.AggregateValidationResults.Any(
                r => r.ValidationResults.Any(
                    v => v.Result == result));
        }
    }
}
