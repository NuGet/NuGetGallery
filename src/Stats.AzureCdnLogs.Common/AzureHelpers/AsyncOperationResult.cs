// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;

namespace Stats.AzureCdnLogs.Common
{
    /// <summary>
    /// A type to capture the operation result status as well as any exception.
    /// </summary>
    public class AsyncOperationResult
    {
        public bool? OperationResult { get; }

        public Exception OperationException { get; }

        public AsyncOperationResult(bool? operationResult, Exception operationException)
        {
            // null are allowed
            OperationResult = operationResult;
            OperationException = operationException;
        }
    }
}
