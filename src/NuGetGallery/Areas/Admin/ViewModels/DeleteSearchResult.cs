// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

namespace NuGetGallery.Areas.Admin.ViewModels
{
    public class DeleteSearchResult
    {
        public string PackageId { get; set; }
        public string PackageVersionNormalized { get; set; }
        public int DownloadCount { get; set; }
        public bool Listed { get; set; }
        public bool Deleted { get; set; }
    }
}