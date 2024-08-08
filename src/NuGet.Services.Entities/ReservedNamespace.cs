// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace NuGet.Services.Entities
{
    public class ReservedNamespace(string value, bool isSharedNamespace, bool isPrefix) : IEntity
    {
        public ReservedNamespace() 
            : this(value: null, isSharedNamespace: false, isPrefix: false)
        {
        }

        [StringLength(Constants.MaxPackageIdLength)]
        [Required]
        public string Value { get; set; } = value;

        public bool IsSharedNamespace { get; set; } = isSharedNamespace;

        public bool IsPrefix { get; set; } = isPrefix;

        public virtual ICollection<PackageRegistration> PackageRegistrations { get; set; } = new HashSet<PackageRegistration>();
        public virtual ICollection<User> Owners { get; set; } = new HashSet<User>();

        [Key]
        public int Key { get; set; }
    }
}