// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;
using Microsoft.VisualStudio.TestTools.WebTesting;
using NuGetGallery.FunctionalTests.Helpers;

namespace NuGetGallery.FunctionalTests.WebUITests.UploadAndDownload
{
    public abstract class UploadPackageFromUI : WebTest
    {
        public UploadPackageFromUI()
        {
            PreAuthenticate = true;
        }
        
        public abstract IEnumerable<UploadHelper.PackageToUpload> PackagesToUpload { get; }

        public override IEnumerator<WebTestRequest> GetRequestEnumerator()
        {
            return UploadHelper.UploadPackages(this, PackagesToUpload);
        }
    }
}
