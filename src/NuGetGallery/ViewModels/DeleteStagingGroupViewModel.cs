// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

namespace NuGetGallery
{
    public class DeleteStagingGroupViewModel
    {
        public string Owner { get; set; }

        public string Id { get; set; }

        public string Name { get; set; }

        public int PackageCount { get; set; }
    }
}
