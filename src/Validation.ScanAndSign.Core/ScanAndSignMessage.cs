// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;

namespace NuGet.Jobs.Validation.ScanAndSign
{
    public class ScanAndSignMessage
    {
        public ScanAndSignMessage(OperationRequestType operationRequestType, Guid packageValidationId, Uri blobUri)
        {
            OperationRequestType = operationRequestType;
            PackageValidationId = packageValidationId;
            BlobUri = blobUri ?? throw new ArgumentNullException(nameof(blobUri));
        }

        public OperationRequestType OperationRequestType { get; }
        public Guid PackageValidationId { get; }
        public Uri BlobUri { get; }
    }
}
