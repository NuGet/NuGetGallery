// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;

namespace NuGetGallery
{
    public class PackageDependent
    {
        public string Id { get; set; }
        public int DownloadCount { get; set; }
        public string Description { get; set; }

        // TODO Add verify checkmark
        // https://github.com/NuGet/NuGetGallery/issues/4718
    }
}
      