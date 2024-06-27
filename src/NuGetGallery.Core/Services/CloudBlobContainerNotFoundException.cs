// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;

namespace NuGetGallery
{
    public class CloudBlobContainerNotFoundException : CloudBlobGenericNotFoundException
    {
        public CloudBlobContainerNotFoundException(Exception innerException)
            : base("Container not found", innerException)
        {
        }
    }
}
