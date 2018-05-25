// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;
using System.Threading.Tasks;

namespace NuGet.Services.Validation.Orchestrator.PackageSigning.ScanAndSign
{
    public interface IScanAndSignEnqueuer
    {
        /// <summary>
        /// Enqueues Scan operation.
        /// </summary>
        /// <param name="request">Request data</param>
        Task EnqueueScanAsync(IValidationRequest request);

        /// <summary>
        /// Enqueues Scan And Sign operation.
        /// </summary>
        /// <param name="request">The requested package validation.</param>
        /// <param name="v3ServiceIndexUrl">The service index URL that should be put on the package's repository signature.</param>
        /// <param name="owners">The list of owners that should be put on the package's repository signature.</param>
        Task EnqueueScanAndSignAsync(IValidationRequest request, string v3ServiceIndexUrl, List<string> owners);
    }
}