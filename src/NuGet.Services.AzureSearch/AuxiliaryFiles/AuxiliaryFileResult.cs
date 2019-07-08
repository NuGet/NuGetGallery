// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;

namespace NuGet.Services.AzureSearch.AuxiliaryFiles
{
    public class AuxiliaryFileResult<T> where T : class
    {
        public AuxiliaryFileResult(
            bool notModified,
            T data,
            AuxiliaryFileMetadata metadata)
        {
            NotModified = notModified;
            if (notModified)
            {
                if (data != null)
                {
                    throw new ArgumentException("The fetched data must be null if it was not modified.", nameof(data));
                }

                if (metadata != null)
                {
                    throw new ArgumentException("The file metadata must be null if it was not modified.", nameof(data));
                }
            }
            else
            {
                Data = data ?? throw new ArgumentNullException(nameof(data));
                Metadata = metadata ?? throw new ArgumentNullException(nameof(metadata));
            }
        }

        public bool NotModified { get; }
        public T Data { get; }
        public AuxiliaryFileMetadata Metadata { get; }
    }
}
