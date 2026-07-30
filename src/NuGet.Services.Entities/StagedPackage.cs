// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.ComponentModel.DataAnnotations;

namespace NuGet.Services.Entities
{
    /// <summary>
    /// Ephemeral staging metadata for a package in <see cref="PackageStatus.Staged"/>. The row is created when the
    /// package is pushed to staging and hard-deleted when the package is promoted, deleted, or expires.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Every staged package has a required <see cref="Owner"/>: ownership is stored here rather than derived from the
    /// package registration's owners, so that a package with no <see cref="StagingGroup"/> is still attributable to an
    /// account. This is an accounting concept, not an authorization one — the right to promote is still evaluated
    /// against the package registration's owners.
    /// </para>
    /// <para>
    /// This data is deliberately kept off the permanent <see cref="Package"/> row because it is meaningless once the
    /// package leaves staging. The durable record of a promotion is <see cref="Package.ApproverUserKey"/>.
    /// </para>
    /// </remarks>
    public class StagedPackage : IEntity
    {
        public const int MaxBlobPathLength = 512;

        /// <summary>
        /// Gets or sets the primary key for the entity.
        /// </summary>
        public int Key { get; set; }

        /// <summary>
        /// Gets or sets the key of the staged package. Unique: a package has at most one staging row.
        /// </summary>
        public int PackageKey { get; set; }

        /// <summary>
        /// Gets or sets the staged package.
        /// </summary>
        public virtual Package Package { get; set; }

        /// <summary>
        /// Gets or sets the key of the user or organization that owns this staged package. This is the account the
        /// package is charged against for <see cref="User.StagingPackageLimit"/>, and it is not the same question as
        /// who owns the package registration: promotion is still authorized against the registration's owners.
        /// </summary>
        public int OwnerKey { get; set; }

        /// <summary>
        /// Gets or sets the user or organization that owns this staged package.
        /// </summary>
        public virtual User Owner { get; set; }

        /// <summary>
        /// Gets or sets the key of the group this package belongs to, or <c>null</c> if the package is ungrouped.
        /// When set, the group's owner must match <see cref="OwnerKey"/>.
        /// </summary>
        public int? StagingGroupKey { get; set; }

        /// <summary>
        /// Gets or sets the group this package belongs to, or <c>null</c> if the package is ungrouped.
        /// </summary>
        public virtual StagingGroup StagingGroup { get; set; }

        /// <summary>
        /// Gets or sets when the package entered staging. The timestamp is in UTC.
        /// </summary>
        public DateTime CreatedDate { get; set; }

        /// <summary>
        /// Gets or sets how far validation has got for this package. A staged package stays in
        /// <see cref="PackageStatus.Staged"/> throughout, so this is the only signal that it is promotable.
        /// </summary>
        public StagedValidationStatus ValidationStatus { get; set; }

        /// <summary>
        /// Gets or sets the tracking ID of the validation set for this row's current content, assigned at push time
        /// and rewritten whenever the content is replaced. Validation outcomes from any other set describe content
        /// that has since been superseded and must be ignored, since a replace reuses the same package key.
        /// </summary>
        public Guid ValidationTrackingId { get; set; }

        /// <summary>
        /// Gets or sets the absolute deadline after which this package is eligible for cleanup. For grouped packages
        /// this mirrors the group's expiration; for ungrouped packages it is set at push time and reset on replace.
        /// The timestamp is in UTC.
        /// </summary>
        public DateTime ExpirationDate { get; set; }

        /// <summary>
        /// Gets or sets the path to the nupkg in the private staging container. The path embeds a GUID so that
        /// deleting and re-uploading the same id and version cannot collide with the outgoing blob.
        /// </summary>
        [Required]
        [StringLength(MaxBlobPathLength)]
        public string BlobPath { get; set; }

        /// <summary>
        /// Gets or sets the path to the companion snupkg in the private staging container, or <c>null</c> if no
        /// symbol package has been staged. Set once symbol validation succeeds.
        /// </summary>
        [StringLength(MaxBlobPathLength)]
        public string SnupkgBlobPath { get; set; }
    }
}
