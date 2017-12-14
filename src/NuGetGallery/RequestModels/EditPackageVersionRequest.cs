// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

namespace NuGetGallery
{
    public class EditPackageVersionReadMeRequest
    {
        public EditPackageVersionReadMeRequest()
        {
            ReadMe = new ReadMeRequest();
        }

        public EditPackageVersionReadMeRequest(PackageEdit pendingMetadata)
        {
            var metadata = pendingMetadata ?? new PackageEdit();

            ReadMeState = metadata.ReadMeState;

            ReadMe = new ReadMeRequest();
        }

        public PackageEditReadMeState ReadMeState { get; set; }

        public ReadMeRequest ReadMe { get; set; }
    }
}