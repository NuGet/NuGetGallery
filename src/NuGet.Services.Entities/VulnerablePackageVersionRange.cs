// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace NuGet.Services.Entities
{
    /// <summary>
    /// Represents an <see cref="PackageRegistration.Id"/> and version range pair that is vulnerable to a <see cref="PackageVulnerability"/>.
    /// </summary>
    public class VulnerablePackageVersionRange : IEntity
    {
        public VulnerablePackageVersionRange()
        {
            Packages = new HashSet<Package>();
        }

        public int Key { get; set; }

        public int VulnerabilityKey { get; set; }
        public PackageVulnerability Vulnerability { get; set; }

        /// <summary>
        /// The ID of the package that is vulnerable.
        /// </summary>
        [StringLength(Constants.MaxPackageIdLength)]
        [Required]
        public string PackageId { get; set; }

        /// <summary>
        /// The version range of <see cref="PackageId"/> that is vulnerable.
        /// </summary>
        /// <remarks>
        /// The version range with the maximum possible length has:
        /// - both a minimum and maximum version with the maximum version length (<see cref="Constants.MaxPackageVersionLength"/> * 2)
        /// - two symbols on both sides for inclusive/exclusive (2)
        /// - the comma and space between the minimum and maximum versions (2)
        /// For a total of <see cref="Constants.MaxPackageVersionLength"/> * 2 + 4.
        /// </remarks>
        [StringLength(Constants.MaxPackageVersionLength * 2 + 4)]
        [Required]
        public string PackageVersionRange { get; set; }

        [StringLength(Constants.MaxPackageVersionLength)]
        public string FirstPatchedPackageVersion { get; set; }

        /// <summary>
        /// The set of packages that is vulnerable.
        /// </summary>
        /// <remarks>
        /// All vulnerable packages should satisfy <see cref="PackageId"/> and <see cref="PackageVersionRange"/>.
        /// </remarks>
        public ICollection<Package> Packages { get; set; }
    }
}