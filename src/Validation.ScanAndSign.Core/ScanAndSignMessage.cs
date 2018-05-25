// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;

namespace NuGet.Jobs.Validation.ScanAndSign
{
    public class ScanAndSignMessage
    {
        public ScanAndSignMessage(
            OperationRequestType operationRequestType,
            Guid packageValidationId,
            Uri blobUri)
        {
            if (operationRequestType == OperationRequestType.Sign)
            {
                throw new ArgumentException($"{nameof(OperationRequestType.Sign)} messages require a V3 service index URL and a list of owners");
            }

            OperationRequestType = operationRequestType;
            PackageValidationId = packageValidationId;
            BlobUri = blobUri ?? throw new ArgumentNullException(nameof(blobUri));
        }

        public ScanAndSignMessage(
            OperationRequestType operationRequestType,
            Guid packageValidationId,
            Uri blobUri,
            string v3ServiceIndexUrl,
            IReadOnlyList<string> owners)
        {
            if (operationRequestType == OperationRequestType.Scan &&
                (!string.IsNullOrEmpty(v3ServiceIndexUrl) || owners != null))
            {
                throw new ArgumentException($"{nameof(OperationRequestType.Scan)} operations do not accept a V3 service index URL or a list of owners");
            }

            OperationRequestType = operationRequestType;
            PackageValidationId = packageValidationId;
            BlobUri = blobUri ?? throw new ArgumentNullException(nameof(blobUri));
            V3ServiceIndexUrl = v3ServiceIndexUrl ?? throw new ArgumentNullException(nameof(v3ServiceIndexUrl));
            Owners = owners ?? throw new ArgumentNullException(nameof(owners));
        }

        public OperationRequestType OperationRequestType { get; }
        public Guid PackageValidationId { get; }
        public Uri BlobUri { get; }
        public string V3ServiceIndexUrl { get; }
        public IReadOnlyList<string> Owners { get; }
    }
}
