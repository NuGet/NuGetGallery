// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.


using System.Collections.Generic;

namespace NuGetGallery.Areas.Admin.ViewModels
{
    public class CorrectIsLatestRequest
    {
        public ICollection<CorrectIsLatestPackageRequest> Packages { get; set; }
    }

    public class CorrectIsLatestPackageRequest
    {
        public string Id { get; set; }
        public string Version { get; set; }
    }
}