// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using Microsoft.WindowsAzure.Storage;
using System;

namespace NuGet.Services.Metadata.Catalog.Persistence
{
    public abstract class DeleteRequestOptions
    {
    }

    /// <summary>
    /// Allows specifying an <see cref="AccessCondition"/> for use by <see cref="AzureStorage"/> in a <see cref="Storage.DeleteAsync(Uri, System.Threading.CancellationToken, DeleteRequestOptions)"/> operation.
    /// </summary>
    public class DeleteRequestOptionsWithAccessCondition : DeleteRequestOptions
    {
        public DeleteRequestOptionsWithAccessCondition(AccessCondition accessCondition)
        {
            AccessCondition = accessCondition ?? throw new ArgumentNullException(nameof(accessCondition));
        }

        public AccessCondition AccessCondition { get; }
    }
}
