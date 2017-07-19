// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace NuGetGallery
{
    public class ReservedNamespace : IEntity
    {
        public ReservedNamespace() 
            : this(value: null, isSharedNamespace: false, isExactMatch: false)
        {
        }

        public ReservedNamespace(string value, bool isSharedNamespace, bool isExactMatch)
        {
            Value = value;
            IsSharedNamespace = isSharedNamespace;
            IsPrefix = isExactMatch;
            PackageRegistrations = new HashSet<PackageRegistration>();
            ReservedNamespaceOwners = new HashSet<User>();
        }

        [StringLength(CoreConstants.MaxPackageIdLength)]
        [Required]
        public string Value { get; set; }

        public bool IsSharedNamespace { get; set; }

        public bool IsPrefix { get; set; }

        public virtual ICollection<PackageRegistration> PackageRegistrations { get; set; }
        public virtual ICollection<User> ReservedNamespaceOwners { get; set; }

        [Key]
        public int Key { get; set; }
    }
}