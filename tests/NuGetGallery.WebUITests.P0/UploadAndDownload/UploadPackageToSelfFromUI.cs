// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;
using NuGetGallery.FunctionalTests.Helpers;

namespace NuGetGallery.FunctionalTests.WebUITests.UploadAndDownload
{
    /// <summary>
    /// Tests uploading a package from the UI.
    /// </summary>
    public class UploadPackageToSelfFromUI : UploadPackageFromUI
    {
        public override IEnumerable<UploadHelper.PackageToUpload> PackagesToUpload => new[]
        {
            new UploadHelper.PackageToUpload(version: "1.0.0"),
            new UploadHelper.PackageToUpload(version: "2.0.0")
        };
    }
}
