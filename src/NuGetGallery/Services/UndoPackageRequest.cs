// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.ComponentModel.DataAnnotations;

namespace NuGetGallery
{
    public class UndoPackageRequest
    {
        [Required]
        public string Id { get; set; }

        [Required]
        public string Version { get; set; }

        public int PackageDeleteKey { get; set; }
    }
}