// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace NuGet.Services.Entities
{
    public class ReservedNamespace : IEntity
    {
        public ReservedNamespace() 
            : this(value: null, isSharedNamespace: false, isPrefix: false)
        {
        }

        public ReservedNamespace(string value, bool isSharedNamespace, bool isPrefix)
        {
            Value = value;
            IsSharedNamespace = isSharedNamespace;
            IsPrefix = isPrefix;
            PackageRegistrations = new HashSet<PackageRegistration>();
            Owners = new HashSet<User>();
        }

        [StringLength(Constants.MaxPackageIdLength)]
        [Required]
        public string Value { get; set; }

        public bool IsSharedNamespace { get; set; }

        public bool IsPrefix { get; set; }

        public virtual ICollection<PackageRegistration> PackageRegistrations { get; set; }
        public virtual ICollection<User> Owners { get; set; }

        [Key]
        public int Key { get; set; }
    }
}