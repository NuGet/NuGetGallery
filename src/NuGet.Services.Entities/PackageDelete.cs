// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace NuGet.Services.Entities
{
    public class PackageDelete
        : IEntity
    {
        public PackageDelete()
        {
            Packages = new HashSet<Package>();
        }

        public int Key { get; set; }

        [Required]
        public DateTime DeletedOn { get; set; }

        /// <remarks>
        /// <c>null</c> if the user was deleted.
        /// </remarks>
        public User DeletedBy { get; set; }

        /// <remarks>
        /// <c>null</c> if the user was deleted.
        /// </remarks>
        public int? DeletedByKey { get; set; }

        [Required]
        public string Reason { get; set; }

        [Required]
        public string Signature { get; set; }

        public virtual ICollection<Package> Packages { get; set; }
    }
}