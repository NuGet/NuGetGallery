// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;

namespace NuGet.Services.Validation.Orchestrator
{
    /// <summary>
    /// Stores the orchestrator configuration
    /// </summary>
    public class ValidationConfiguration
    {
        /// <summary>
        /// List of validations to run with their settings and dependencies
        /// </summary>
        public List<ValidationConfigurationItem> Validations { get; set; }

        /// <summary>
        /// Connection string to storage account with packages to validate
        /// </summary>
        public string ValidationStorageConnectionString { get; set; }

        /// <summary>
        /// How many times the Orchestrator should retry to validate a package
        /// that is missing from the Gallery database.
        /// </summary>
        public int MissingPackageRetryCount { get; set; }

        /// <summary>
        /// Time to wait between checking the state of a certain validation.
        /// </summary>
        public TimeSpan ValidationMessageRecheckPeriod { get; set; }

        /// <summary>
        /// The duplication detection window used for new validation requests.
        /// Validation requests will be ignored if there exists another validation
        /// request for the same package id and version that was created within
        /// this window.
        /// </summary>
        public TimeSpan NewValidationRequestDeduplicationWindow { get; set; }

        /// <summary>
        /// The threshold until which an email will be sent out due to a validation set taking too long.
        /// </summary>
        public TimeSpan ValidationSetNotificationTimeout { get; set; }

        /// <summary>
        /// The threshold until a validation set is no longer processed.
        /// </summary>
        public TimeSpan TimeoutValidationSetAfter { get; set; }

        /// <summary>
        /// The duration for which SAS tokens are generated for package URLs passed down to validators.
        /// </summary>
        public TimeSpan NupkgUrlValidityPeriod { get; set; } = TimeSpan.FromDays(7);
    }
}
