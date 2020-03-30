// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace NuGet.Services.Entities
{
    public class PackageRegistration
        : IEntity
    {
        public PackageRegistration()
        {
            Owners = new HashSet<User>();
            Packages = new HashSet<Package>();
            ReservedNamespaces = new HashSet<ReservedNamespace>();
            RequiredSigners = new HashSet<User>();
            AlternativeOf = new HashSet<PackageDeprecation>();
            FromPackageRenames = new HashSet<PackageRenames>();
            ToPackageRenames = new HashSet<PackageRenames>();
        }

        [StringLength(Constants.MaxPackageIdLength)]
        [Required]
        public string Id { get; set; }

        public int DownloadCount { get; set; }

        public bool IsVerified { get; set; }

        public bool IsLocked { get; set; }

        public virtual ICollection<User> Owners { get; set; }
        public virtual ICollection<Package> Packages { get; set; }
        public virtual ICollection<ReservedNamespace> ReservedNamespaces { get; set; }

        /// <summary>
        /// Gets or sets required signers for this package registration.
        /// </summary>
        public virtual ICollection<User> RequiredSigners { get; set; }

        public int Key { get; set; }

        /// <summary>
        /// Gets and sets the list of deprecations that recommend this package registration as an alternative.
        /// See <see cref="PackageDeprecation.AlternatePackageRegistration"/>.
        /// </summary>
        public virtual ICollection<PackageDeprecation> AlternativeOf { get; set; }

        /// <summary>
        /// Gets or sets the list of package registrations that were renamed.
        /// </summary>
        public ICollection<PackageRenames> FromPackageRenames { get; set; }

        /// <summary>
        /// Gets or sets the list of replacement package registrations.
        /// </summary>
        public ICollection<PackageRenames> ToPackageRenames { get; set; }

        /// <summary>
        /// Gets or sets the user-provided custom message for this renamed package registration.
        /// </summary>
        public string RenamedMessage { get; set; }
    }
}