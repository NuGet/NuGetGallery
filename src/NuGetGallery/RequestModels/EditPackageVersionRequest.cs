// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

namespace NuGetGallery
{
    public class EditPackageVersionRequest
    {
        public EditPackageVersionRequest()
        {
            ReadMe = new ReadMeRequest();
        }

        public EditPackageVersionRequest(PackageEdit pendingMetadata)
        {
            var metadata = pendingMetadata ?? new PackageEdit();

            ReadMeState = metadata.ReadMeState;

            ReadMe = new ReadMeRequest();
        }

        public PackageEditReadMeState ReadMeState { get; set; }

        public ReadMeRequest ReadMe { get; set; }
    }
}