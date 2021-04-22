// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;

namespace NuGet.Jobs.Validation.ContentScan
{
    public class StartContentScanData
    {
        public StartContentScanData(
            Guid validationTrackingId,
            Uri blobUri)
        {
            ValidationTrackingId = validationTrackingId;
            BlobUri = blobUri ?? throw new ArgumentNullException(nameof(blobUri));
        }

        public Guid ValidationTrackingId { get; }
        public Uri BlobUri { get; }
    }
}
