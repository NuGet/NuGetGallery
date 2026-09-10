// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.ComponentModel.DataAnnotations;

namespace NuGet.Services.Entities
{
    /// <summary>
    /// Represents an owner-scoped group of staged package identities.
    /// </summary>
    public class StagingGroup : IEntity
    {
        public int Key { get; set; }

        public int OwnerKey { get; set; }

        public virtual User Owner { get; set; }

        [Required]
        [StringLength(64)]
        public string Id { get; set; }

        [Required]
        [StringLength(128)]
        public string Name { get; set; }

        public DateTime CreatedDate { get; set; }
    }
}
