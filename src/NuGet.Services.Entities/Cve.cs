// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace NuGet.Services.Entities
{
    /// <summary>
    /// Represents a Common Vulnerability and Exposure (CVE).
    /// </summary>
    public class Cve
        : IEntity
    {
        public Cve()
        {
            PackageDeprecations = new HashSet<PackageDeprecation>();
        }

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
        public string CveId { get; set; }

        /// <summary>
        /// Gets or sets the description of the CVE.
        /// The description is a plain language field that describes the vulnerability with sufficient detail as to demonstrate that the vulnerability is unique.
        /// </summary>
        /// <remarks>
        /// The description field is intentionally truncated to maximum 300 characters.
        /// </remarks>
        [MaxLength(300)]
        [Required]
        public string Description { get; set; }

        /// <summary>
        /// Gets or sets the last-modified date for the entity.
        /// </summary>
        public DateTime LastModifiedDate { get; set; }

        /// <summary>
        /// Gets or sets the date this CVE entity was first published.
        /// </summary>
        public DateTime PublishedDate { get; set; }

        /// <summary>
        /// Gets or sets whether the <see cref="Cve"/> is publicly listed.
        /// An unlisted CVE is no longer available for reference.
        /// Any <see cref="PackageDeprecation"/>s referencing an unlisted CVE will maintain existing references.
        /// </summary>
        public bool Listed { get; set; }

        /// <summary>
        /// Gets or sets the status of the <see cref="Cve"/>.
        /// </summary>
        [Required]
        public CveStatus Status { get; set; }

        /// <summary>
        /// Gets or sets the CVSS rating for this <see cref="Cve"/> as determined by the NVD.
        /// </summary>
        /// <remarks>
        /// CVSS ratings are from 0.0 to 10.0 and have a single point of precision.
        /// </remarks>
        [Range(0, 10)]
        public decimal? CvssRating { get; set; }

        public virtual ICollection<PackageDeprecation> PackageDeprecations { get; set; }
    }
}