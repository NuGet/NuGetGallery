// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;
using Microsoft.VisualStudio.TestTools.WebTesting;
using NuGetGallery.FunctionalTests.Helpers;

namespace NuGetGallery.FunctionalTests.WebUITests.UploadAndDownload
{
    /// <summary>
    /// Uploads a new version of an existing package.
    /// </summary>
    public class UploadPackageWithNewVersionFromUI : WebTest
    {
        public UploadPackageWithNewVersionFromUI()
        {
            PreAuthenticate = true;
        }

        public override IEnumerator<WebTestRequest> GetRequestEnumerator()
        {
            return UploadHelper.UploadPackages(this,
                new UploadHelper.PackageToUpload(version: "1.0.0"),
                new UploadHelper.PackageToUpload(version: "2.0.0"));
        }
    }
}
