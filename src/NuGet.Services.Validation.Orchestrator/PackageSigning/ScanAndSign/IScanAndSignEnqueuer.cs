// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Threading.Tasks;
using NuGet.Jobs.Validation.ScanAndSign;

namespace NuGet.Services.Validation.Orchestrator.PackageSigning.ScanAndSign
{
    public interface IScanAndSignEnqueuer
    {
        /// <summary>
        /// Enqueues Scan operation.
        /// </summary>
        /// <param name="request">Request data</param>
        Task EnqueueScanAsync(IValidationRequest request);
    }
}