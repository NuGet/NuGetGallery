﻿// Copyright (c) .NET Foundation. All rights reserved.
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
        private static string Owner = EnvironmentSettings.TestOrganizationCollaboratorAccountName;

        private string _id = UploadHelper.GetUniquePackageId(nameof(UploadPackageToOrganizationAsCollaboratorFromUI));

        public override IEnumerable<UploadHelper.PackageToUpload> PackagesToUpload => new[]
        {
            // Upload new registration
            new UploadHelper.PackageToUpload(id: _id, version: "1.0.0", owner: Owner),
            
            // Upload new version of existing registration
            new UploadHelper.PackageToUpload(id: _id, version: "2.0.0", owner: Owner)
        };
    }
}
