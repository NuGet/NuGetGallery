// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

namespace NuGetGallery
{
    public class SubmitPackageRequest
    {
        public bool IsUploadInProgress => InProgressUpload != null;

        public VerifyPackageRequest InProgressUpload { get; set; }

        public bool IsSymbolsUploadEnabled { get; set; }

        public bool AreUploadEmbeddedReadmesEnabled { get; set; }
    }
}