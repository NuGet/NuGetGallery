// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace NuGetGallery
{
    public class ReservedPrefix : IEntity
    {
        public ReservedPrefix() : this(null, false)
        {
        }

        public ReservedPrefix(string pattern, bool isPublicNamespace)
        {
            Pattern = pattern;
            IsPublicNamespace = isPublicNamespace;
            PackageRegistrations = new HashSet<PackageRegistration>();
            ReservedPrefixOwners = new HashSet<User>();
        }

        [StringLength(CoreConstants.MaxPackageIdLength)]
        [Required]
        public string Pattern { get; set; }
        public bool IsPublicNamespace { get; set; }
        public virtual ICollection<PackageRegistration> PackageRegistrations { get; set; }
        public virtual ICollection<User> ReservedPrefixOwners { get; set; }

        [Key]
        public int Key { get; set; }
    }
}