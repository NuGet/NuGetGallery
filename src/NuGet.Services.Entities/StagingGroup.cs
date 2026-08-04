// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace NuGet.Services.Entities
{
    public class StagingGroup : IEntity
    {
        public StagingGroup()
        {
            Entries = new HashSet<StagingEntry>();
        }

        public int Key { get; set; }

        /// <summary>
        /// Gets or sets the account that owns this staging workspace and pays its quota. Package authorization
        /// continues to use the potentially multiple owners on each package registration.
        /// </summary>
        public int OwnerKey { get; set; }

        /// <summary>
        /// Gets or sets the account identified by <see cref="OwnerKey"/>.
        /// </summary>
        public User Owner { get; set; }

        [Required]
        [StringLength(64)]
        public string Id { get; set; }

        [Required]
        [StringLength(256)]
        public string Name { get; set; }

        public DateTime CreatedDate { get; set; }

        public DateTime ExpirationDate { get; set; }

        public byte[] RowVersion { get; set; }

        public virtual ICollection<StagingEntry> Entries { get; set; }
    }
}
