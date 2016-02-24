// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace NuGet.Services.Gallery
{
    public class PackageRegistration
        : IEntity
    {
        public const int MaxPackageIdLength = 128;

        public PackageRegistration()
        {
            Owners = new HashSet<User>();
            Packages = new HashSet<Package>();
        }

        [StringLength(MaxPackageIdLength)]
        [Required]
        public string Id { get; set; }

        public int DownloadCount { get; set; }
        public virtual ICollection<User> Owners { get; set; }
        public virtual ICollection<Package> Packages { get; set; }
        public int Key { get; set; }
    }
}