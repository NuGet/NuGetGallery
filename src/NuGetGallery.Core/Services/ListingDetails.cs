// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;

namespace NuGetGallery
{
    [Flags]
    public enum ListingDetails
    {
        None = 0,
        Snapshots = 1,
        Metadata = 2,
        UncommittedBlobs = 4,
        Copy = 8,
        Deleted = 0x10,
        All = 0x1F
    }
}
