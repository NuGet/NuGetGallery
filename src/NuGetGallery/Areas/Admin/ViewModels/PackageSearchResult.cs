// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;

namespace NuGetGallery.Areas.Admin.ViewModels
{
    public class PackageSearchResult
    {
        public string PackageId { get; set; }
        public string PackageVersionNormalized { get; set; }
        public int DownloadCount { get; set; }
        public string Created { get; set; }
        public bool Listed { get; set; }
        public string PackageStatus { get; set; }
        public IReadOnlyList<UserViewModel> Owners { get; set; }
    }
}