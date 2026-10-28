// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;

namespace NuGetGallery
{
    public class UndoSoftDeleteBatchResult
    {
        public bool Success => Exception == null;
        public UndoPackageRequest FailedPackage { get; }
        public Exception Exception { get; }

        public UndoSoftDeleteBatchResult()
        {
        }

        public UndoSoftDeleteBatchResult(UndoPackageRequest failedPackage, Exception exception)
        {
            FailedPackage = failedPackage ?? throw new ArgumentNullException(nameof(failedPackage));
            Exception = exception ?? throw new ArgumentNullException(nameof(exception));
        }
    }
}