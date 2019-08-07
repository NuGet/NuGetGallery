// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace NuGet.Services.Entities
{
    /// <summary>
    /// Represents a package deprecation.
    /// </summary>
    public class PackageDeprecation
        : IEntity
    {
        public const int MaxCustomMessageLength = 1000;

        public PackageDeprecation()
        {
        }

        /// <summary>
        /// Gets or sets the primary key for the entity.
        /// </summary>
        public int Key { get; set; }
        
        /// <summary>
        /// Gets or sets the package affected by this deprecation.
        /// </summary>
        public virtual Package Package { get; set; }

        /// <summary>
        /// Gets or sets the key of the package affected by this deprecation.
        /// </summary>
        [Index(IsUnique = true)]
        public int PackageKey { get; set; }

        /// <summary>
        /// Gets or sets the status of this deprecation.
        /// </summary>
        /// <remarks>
        /// A <see cref="PackageDeprecation"/> with a <see cref="PackageDeprecationStatus"/> of <see cref="PackageDeprecationStatus.NotDeprecated"/> is meaningless, so <see cref="Status"/> must be at least <c>1</c>.
        /// </remarks>
        [Range(1, int.MaxValue)]
        public PackageDeprecationStatus Status { get; set; }

        /// <summary>
        /// Gets or sets the alternate package registration entity.
        /// It is recommended that users use this package registration instead of the deprecated package.
        /// </summary>
        public virtual PackageRegistration AlternatePackageRegistration { get; set; }

        /// <summary>
        /// Gets or sets the alternate package registration entity key.
        /// It is recommended that users use this package registration instead of the deprecated package.
        /// </summary>
        public int? AlternatePackageRegistrationKey { get; set; }

        /// <summary>
        /// Gets or sets the alternate package entity.
        /// It is recommended that users use this package instead of the deprecated package.
        /// </summary>
        public virtual Package AlternatePackage { get; set; }

        /// <summary>
        /// Gets or sets the alternate package entity key.
        /// It is recommended that users use this package instead of the deprecated package.
        /// </summary>
        public int? AlternatePackageKey { get; set; }

        /// <summary>
        /// Gets or sets the user that executed the package deprecation.
        /// </summary>
        /// <remarks>
        /// This field will be <c>null</c> if the user that deprecated the package has been deleted.
        /// </remarks>
        public virtual User DeprecatedByUser { get; set; }

        /// <summary>
        /// Gets or sets the key of the user that executed the package deprecation.
        /// </summary>
        public int? DeprecatedByUserKey { get; set; }

        /// <summary>
        /// The date when the package was deprecated.
        /// </summary>
        [DatabaseGenerated(DatabaseGeneratedOption.Computed)]
        public DateTime DeprecatedOn { get; set; }

        /// <summary>
        /// Gets or sets the user-provided custom message for this package deprecation.
        /// </summary>
        public string CustomMessage { get; set; }
    }
}