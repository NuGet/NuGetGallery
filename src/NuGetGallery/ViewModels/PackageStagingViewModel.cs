// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;

namespace NuGetGallery
{
    public class PackageStagingViewModel
    {
        public string Id { get; set; }

        public string Version { get; set; }

        public string Owner { get; set; }

        public string Status { get; set; }

        public DateTime UploadedDate { get; set; }
    }
}
