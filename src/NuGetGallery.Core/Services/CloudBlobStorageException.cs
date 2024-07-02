// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;

namespace NuGetGallery
{
    public class CloudBlobStorageException : Exception
    {
        public CloudBlobStorageException(Exception innerException)
            : base(innerException?.Message ?? string.Empty, innerException)
        {
        }

        public CloudBlobStorageException(string message, Exception innerException)
            : base(message, innerException)
        {
        }

        public CloudBlobStorageException(string message)
            : base(message)
        {
        }
    }
}
