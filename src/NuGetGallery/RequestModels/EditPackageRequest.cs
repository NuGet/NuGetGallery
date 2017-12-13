// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;
using System.Web.Mvc;

namespace NuGetGallery
{
    public class EditPackageRequest
    {
        public SelectList VersionSelectList { get; set; }

        public EditPackageVersionReadMeRequest Edit { get; set; }

        public string PackageId { get; set; }
        public string PackageTitle { get; set; }
        public string Version { get; set; }
        public IList<Package> PackageVersions { get; set; }
        public bool IsLocked { get; set; }
    }
}