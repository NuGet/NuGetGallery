﻿// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace NuGetGallery
{
    public class PackageRegistration
        : IEntity
    {
        public PackageRegistration()
        {
            Owners = new HashSet<User>();
            Packages = new HashSet<Package>();
            ReservedNamespaces = new HashSet<ReservedNamespace>();
            RequiredSigners = new HashSet<User>();
        }

        [StringLength(CoreConstants.MaxPackageIdLength)]
        [Required]
        public string Id { get; set; }

        public int DownloadCount { get; set; }

        public bool IsVerified { get; set; }

        public bool IsLocked { get; set; }

        public virtual ICollection<User> Owners { get; set; }
        public virtual ICollection<Package> Packages { get; set; }
        public virtual ICollection<ReservedNamespace> ReservedNamespaces { get; set; }

        /// <summary>
        /// Gets or sets required signers for this package registration.
        /// </summary>
        public virtual ICollection<User> RequiredSigners { get; set; }

        public int Key { get; set; }
    }
}