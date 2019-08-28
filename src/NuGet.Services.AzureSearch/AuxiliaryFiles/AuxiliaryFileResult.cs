// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;

namespace NuGet.Services.AzureSearch.AuxiliaryFiles
{
    public class AuxiliaryFileResult<T> where T : class
    {
        public AuxiliaryFileResult(
            bool modified,
            T data,
            AuxiliaryFileMetadata metadata)
        {
            Modified = modified;
            if (modified)
            {
                Data = data ?? throw new ArgumentNullException(nameof(data));
                Metadata = metadata ?? throw new ArgumentNullException(nameof(metadata));
            }
            else
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
        }

        /// <summary>
        /// Whether or not the data has been modified since the last time this result was fetched. This can be set to
        /// false by an auxiliary file client by reading an endpoint with an etag, i.e. with the <c>If-Match:</c> HTTP
        /// request header.
        /// </summary>
        public bool Modified { get; }

        /// <summary>
        /// The data in the auxiliary file. This will be non-null if <see cref="Modified"/> is true and null if it is
        /// false.
        /// </summary>
        public T Data { get; }

        /// <summary>
        /// The metadata about the auxiliary file for no-op and diagnostics purposes. This type has the etag which can
        /// be used to only download the data if it has changed.
        /// </summary>
        public AuxiliaryFileMetadata Metadata { get; }
    }
}
