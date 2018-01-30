// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using NuGetGallery.FunctionalTests.Helpers;
using System.Collections.Generic;

namespace NuGetGallery.FunctionalTests.WebUITests.UploadAndDownload
{
    /// <summary>
    /// Tests uploading a package from the UI as an organization collaborator.
    /// </summary>
    public class UploadPackageToOrganizationAsCollaboratorFromUI : UploadPackageFromUI
    {
        public override IEnumerable<UploadHelper.PackageToUpload> PackagesToUpload => new[]
        {
            new UploadHelper.PackageToUpload(
                    id: Constants.TestOrganizationCollaboratorPackageId,
                    owner: EnvironmentSettings.TestOrganizationCollaboratorAccountName)
        };
    }
}
