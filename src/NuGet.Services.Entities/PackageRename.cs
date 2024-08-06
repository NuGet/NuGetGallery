// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.ComponentModel.DataAnnotations.Schema;

namespace NuGet.Services.Entities
{
    public class PackageRename : IEntity
    {
        /// <summary>
        /// Gets or sets the primary key for the entity.
        /// </summary>
        public int Key { get; set; }

        /// <summary>
        /// Gets or sets the renamed package registration entity key.
        /// </summary>
        public int FromPackageRegistrationKey { get; set; }

        /// <summary>
        /// Gets or sets the renamed package registration entity.
        /// </summary>
        public virtual PackageRegistration FromPackageRegistration { get; set; }

        /// <summary>
        /// Gets or sets the replacement package registration entity key.
        /// </summary>
        public int ToPackageRegistrationKey { get; set; }

        /// <summary>
        /// Gets or sets the replacement package registration entity.
        /// </summary>
        public virtual PackageRegistration ToPackageRegistration { get; set; }

        /// <summary>
        /// Gets or sets whether the renamed package's popularity should be transferred to the replacement packages.
        /// </summary>
        public bool TransferPopularity { get; set; }

        [DatabaseGenerated(DatabaseGeneratedOption.Computed)]
        public DateTime UpdatedOn { get; set; }
    }
}
