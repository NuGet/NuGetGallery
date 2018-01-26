// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;
using Microsoft.VisualStudio.TestTools.WebTesting;
using NuGetGallery.FunctionalTests.Helpers;

namespace NuGetGallery.FunctionalTests.WebUITests.UploadAndDownload
{
    /// <summary>
    /// Uploads a package with a new ID.
    /// </summary>
    public class UploadPackageWithNewIdFromUI : WebTest
    {
        public UploadPackageWithNewIdFromUI()
        {
            PreAuthenticate = true;
        }

        public override IEnumerator<WebTestRequest> GetRequestEnumerator()
        {
            return UploadHelper.UploadPackage(this, EnvironmentSettings.TestAccountName);
        }
    }
}
