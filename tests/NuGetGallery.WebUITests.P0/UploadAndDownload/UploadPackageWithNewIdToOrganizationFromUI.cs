// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using Microsoft.VisualStudio.TestTools.WebTesting;
using NuGetGallery.FunctionalTests.Helpers;

namespace NuGetGallery.FunctionalTests.WebUITests.UploadAndDownload
{
    /// <summary>
    /// Uploads a new test package using Gallery UI. Validates that logon prompt appears to upload and checks that the package's home page opens post upload.
    /// priority : p0
    /// </summary>
    public class UploadPackageWithNewIdToOrganizationFromUI : WebTest
    {
        public UploadPackageWithNewIdToOrganizationFromUI()
        {
            PreAuthenticate = true;
        }

        public override IEnumerator<WebTestRequest> GetRequestEnumerator()
        {
            return UploadHelper.UploadPackage(this, EnvironmentSettings.TestOrganizationAdminAccountName);
        }
    }
}
