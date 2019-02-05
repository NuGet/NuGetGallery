// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
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
        /// Gets or sets the unique CVE ID.
        /// The CVE ID number has four or more digits in the sequence number portion of the ID (e.g., "CVE-1999-0067", "CVE-2014-12345", "CVE-2016-7654321").
        /// </summary>
        // sources:
        //  * https://cve.mitre.org/cve/identifiers/syntaxchange.html
        //  * https://cve.mitre.org/about/faqs.html#what_is_cve_id
        [Index(IsUnique = true)]
        [Required]
        [MaxLength(20)]
        public string CVEId { get; set; }

        /// <summary>
        /// Gets or sets the description of the CVE.
        /// The description is a plain language field that describes the vulnerability with sufficient detail as to demonstrate that the vulnerability is unique.
        /// </summary>
        [MaxLength(4000)]
        [Required]
        public string Description { get; set; }

        public virtual ICollection<PackageDeprecation> PackageDeprecations { get; set; }
    }
}