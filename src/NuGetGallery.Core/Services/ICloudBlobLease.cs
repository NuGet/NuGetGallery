// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using Azure;

namespace NuGetGallery
{
    public interface ICloudBlobLease
    {
        public ETag ETag { get; }

        public DateTimeOffset LastModified { get; }

        public string LeaseId { get; }

        public int? LeaseTime { get; }
    }
}
