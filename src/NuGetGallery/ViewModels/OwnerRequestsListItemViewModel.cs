// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using NuGet.Services.Entities;

namespace NuGetGallery
{
    public class OwnerRequestsListItemViewModel
    {
        public PackageOwnerRequest Request { get; set; }

        public ListPackageItemViewModel Package { get; set; }

        public bool CanAccept { get; set; }

        public bool CanCancel { get; set; }

        public bool IsExpired { get; set; }
    }
}