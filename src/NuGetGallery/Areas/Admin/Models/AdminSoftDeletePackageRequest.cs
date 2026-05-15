// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;

namespace NuGetGallery.Areas.Admin.Models
{
    public class AdminSoftDeletePackageRequest
    {
        public List<AdminSoftDeletePackageIdentity> Packages { get; set; }

        public string Reason { get; set; }
    }

    public class AdminSoftDeletePackageIdentity
    {
        public string Id { get; set; }

        public string Version { get; set; }
    }
}
