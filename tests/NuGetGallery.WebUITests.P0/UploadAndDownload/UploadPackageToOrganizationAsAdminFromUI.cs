// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;
using Microsoft.VisualStudio.TestTools.WebTesting;
using NuGetGallery.FunctionalTests.Helpers;

namespace NuGetGallery.FunctionalTests.WebUITests.UploadAndDownload
{
    /// <summary>
    /// Tests uploading a package from the UI as an organization admin.
    /// </summary>
    public class UploadPackageToOrganizationFromUI : UploadPackageFromUI
    {
        public override IEnumerable<UploadHelper.PackageToUpload> PackagesToUpload => new[]
        {
            new UploadHelper.PackageToUpload(
                owner: EnvironmentSettings.TestOrganizationAdminAccountName),

            new UploadHelper.PackageToUpload(
                id: Constants.TestOrganizationAdminPackageId, 
                owner: EnvironmentSettings.TestOrganizationAdminAccountName)
        };
    }
}
