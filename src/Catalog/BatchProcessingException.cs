// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;

namespace NuGet.Services.Metadata.Catalog
{
    public sealed class BatchProcessingException : Exception
    {
        public BatchProcessingException(Exception inner)
            : base(Strings.BatchProcessingFailure, inner ?? throw new ArgumentNullException(nameof(inner)))
        {
        }
    }
}