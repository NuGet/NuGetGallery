// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.IO;

namespace NuGetGallery
{
    public class StagingUploadRequest
    {
        public Stream Package { get; set; }
        public Stream Symbols { get; set; }
        public string GroupId { get; set; }
        public bool? Listed { get; set; }
    }
}
