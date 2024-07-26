// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;

namespace NuGet.Jobs.Validation.ContentScan
{
    public class StartContentScanData
    {
        public StartContentScanData(
           Guid validationStepId,
           Uri blobUri)
        {
            ValidationStepId = validationStepId;
            BlobUri = blobUri ?? throw new ArgumentNullException(nameof(blobUri));
        }

        public StartContentScanData(
            Guid validationStepId,
            Uri blobUri,
            string contentType)
        {
            ValidationStepId = validationStepId;
            BlobUri = blobUri ?? throw new ArgumentNullException(nameof(blobUri));
            ContentType = contentType ?? throw new ArgumentNullException(nameof(contentType));
        }

        public Guid ValidationStepId { get; }
        public Uri BlobUri { get; }
        public String ContentType { get; }
    }
}
