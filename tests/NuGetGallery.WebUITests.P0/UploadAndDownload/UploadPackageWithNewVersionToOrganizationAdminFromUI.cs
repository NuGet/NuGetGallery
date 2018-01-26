// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using Microsoft.VisualStudio.TestTools.WebTesting;
using NuGetGallery.FunctionalTests.Helpers;
using System.Collections.Generic;

namespace NuGetGallery.FunctionalTests.WebUITests.UploadAndDownload
{
    /// <summary>
    /// Uploads a new version of an existing package owned by an organization as one of its admins.
    /// </summary>
    public class UploadPackageWithNewVersionToOrganizationAdminFromUI : WebTest
    {
        public UploadPackageWithNewVersionToOrganizationAdminFromUI()
        {
            PreAuthenticate = true;
        }

        public override IEnumerator<WebTestRequest> GetRequestEnumerator()
        {
            return UploadHelper.UploadPackages(this, 
                new UploadHelper.PackageToUpload(
                    id: EnvironmentSettings.TestOrganizationAdminAccountPackageId, 
                    owner: EnvironmentSettings.TestOrganizationAdminAccountName));
        }
    }
}
