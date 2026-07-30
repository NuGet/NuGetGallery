// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace NuGet.Services.Entities
{
    /// <summary>
    /// A named collection of staged packages belonging to a single owner. Groups let an author stage a set of
    /// interdependent packages and promote them together, so consumers never observe a partially published set.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Groups are optional. A staged package with no group has a <c>null</c> <see cref="StagedPackage.StagingGroupKey"/>.
    /// A staged package belongs to at most one group, enforced by the unique index on
    /// <see cref="StagedPackage.PackageKey"/>.
    /// </para>
    /// <para>
    /// Groups are hard-deleted. Deleting a group removes its <see cref="StagedPackage"/> rows and enqueues their
    /// blobs to <see cref="StagingBlobCleanup"/> in the same transaction, so the group row has no remaining
    /// dependents and there is nothing left for the cleanup job to defer.
    /// </para>
    /// </remarks>
    public class StagingGroup : IEntity
    {
        public const int MaxIdLength = 64;
        public const int MaxNameLength = 256;

        /// <summary>
        /// Gets or sets the primary key for the entity.
        /// </summary>
        public int Key { get; set; }

        /// <summary>
        /// Gets or sets the user-specified slug for this group: lowercase alphanumeric characters and dashes.
        /// Unique per owner, not globally.
        /// </summary>
        [Required]
        [StringLength(MaxIdLength)]
        public string Id { get; set; }

        /// <summary>
        /// Gets or sets the key of the user or organization that owns this group.
        /// </summary>
        public int OwnerKey { get; set; }

        /// <summary>
        /// Gets or sets the user or organization that owns this group. Any user with push permissions under this
        /// owner may add packages to the group, promote it, or delete it.
        /// </summary>
        public virtual User Owner { get; set; }

        /// <summary>
        /// Gets or sets the display name for this group. Set to <see cref="Id"/> when the author does not supply one.
        /// </summary>
        [Required]
        [StringLength(MaxNameLength)]
        public string Name { get; set; }

        /// <summary>
        /// Gets or sets when the group was created. The timestamp is in UTC.
        /// </summary>
        public DateTime CreatedDate { get; set; }

        /// <summary>
        /// Gets or sets the absolute deadline after which this group is eligible for cleanup. Set on creation and
        /// reset on every push to the group. The timestamp is in UTC.
        /// </summary>
        public DateTime ExpirationDate { get; set; }

        /// <summary>
        /// Gets or sets the packages staged in this group.
        /// </summary>
        public virtual ICollection<StagedPackage> StagedPackages { get; set; }
    }
}
