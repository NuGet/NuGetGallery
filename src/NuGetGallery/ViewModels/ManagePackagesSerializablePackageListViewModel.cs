// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;

namespace NuGetGallery
{
    public class ManagePackagesSerializablePackageListViewModel
    {
        public ManagePackagesSerializablePackageListViewModel(string error)
        {
            Error = error;
        }

        public ManagePackagesSerializablePackageListViewModel(
            int totalCount, 
            int totalDownloadCount, 
            IEnumerable<ManagePackagesSerializablePackageViewModel> packages)
        {
            TotalCount = totalCount;
            TotalDownloadCount = totalDownloadCount;
            Packages = packages;
        }

        public string Error { get; }
        public int? TotalCount { get; }
        public int? TotalDownloadCount { get; }
        public IEnumerable<ManagePackagesSerializablePackageViewModel> Packages { get; }
    }
}