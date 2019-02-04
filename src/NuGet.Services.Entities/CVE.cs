// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;

namespace NuGet.Services.Entities
{
    /// <summary>
    /// Represents a Common Vulnerability and Exposure (CVE).
    /// </summary>
    public class CVE
        : IEntity
    {
        /// <summary>
        /// Gets or sets the primary key for the entity.
        /// </summary>
        public int Key { get; set; }

        /// <summary>
        /// Gets or sets the unique CVE-ID.
        /// </summary>
        [Index(IsUnique = true)]
        public string CVEId { get; set; }

        /// <summary>
        /// Gets or sets the description of the CWE.
        /// </summary>
        public string Description { get; set; }

        public virtual ICollection<PackageDeprecation> PackageDeprecations { get; set; }
    }
}