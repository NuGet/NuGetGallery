// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace NuGet.Services.Entities
{
    /// <summary>
    /// Represents a Common Weakness Enumeration (CWE).
    /// </summary>
    public class Cwe
        : IEntity
    {
        public Cwe()
        {
            PackageDeprecations = new HashSet<PackageDeprecation>();
        }

        /// <summary>
        /// Gets or sets the primary key for the entity.
        /// </summary>
        public int Key { get; set; }

        /// <summary>
        /// Gets or sets the unique CWE-ID.
        /// </summary>
        [Index(IsUnique = true)]
        [Required]
        [MaxLength(20)]
        public string CweId { get; set; }

        /// <summary>
        /// Gets or sets the name of the CWE.
        /// </summary>
        [MaxLength(200)]
        [Required]
        public string Name { get; set; }

        /// <summary>
        /// Gets or sets the description of the CWE.
        /// </summary>
        /// <remarks>
        /// The description field is intentionally truncated to maximum 300 characters.
        /// </remarks>
        [MaxLength(300)]
        [Required]
        public string Description { get; set; }

        public virtual ICollection<PackageDeprecation> PackageDeprecations { get; set; }
    }
}