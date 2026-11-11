// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;

namespace NuGetGallery
{
    public class UndoSoftDeleteBatchResult
    {
        public IReadOnlyList<UndoSoftDeletePackageResult> Results { get; }
        public bool Success => Results.All(result => result.Success);
        public int RestoredCount => Results.Count(result => result.Success);

        public UndoSoftDeleteBatchResult(IReadOnlyList<UndoSoftDeletePackageResult> results)
        {
            Results = results ?? throw new ArgumentNullException(nameof(results));
        }
    }

    public class UndoSoftDeletePackageResult
    {
        public UndoPackageRequest Package { get; }
        public Exception Exception { get; }
        public bool Success => Exception == null;

        public UndoSoftDeletePackageResult(UndoPackageRequest package, Exception exception = null)
        {
            Package = package ?? throw new ArgumentNullException(nameof(package));
            Exception = exception;
        }
    }
}