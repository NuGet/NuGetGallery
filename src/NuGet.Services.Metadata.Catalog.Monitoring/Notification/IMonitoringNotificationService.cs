// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;

namespace NuGet.Services.Metadata.Catalog.Monitoring
{
    /// <summary>
    /// Determines behavior of the monitoring job after a package has been validated or validation failed to run on that package.
    /// </summary>
    public interface IMonitoringNotificationService
    {
        /// <summary>
        /// Called whenever validation finishes on a package.
        /// </summary>
        /// <param name="result">Result of the validation.</param>
        Task OnPackageValidationFinishedAsync(PackageValidationResult result, CancellationToken token);

        /// <summary>
        /// Called whenever validation failed to run on a package.
        /// </summary>
        /// <param name="packageId">Id of the package that could not be validated.</param>
        /// <param name="packageVersion">Version of the package that could not be validated.</param>
        /// <param name="catalogEntriesJson">Catalog entries of the package that queued the validation.</param>
        /// <param name="e">Exception that was thrown while running validation on the package.</param>
        Task OnPackageValidationFailedAsync(string packageId, string packageVersion, Exception e, CancellationToken token);
    }
}
