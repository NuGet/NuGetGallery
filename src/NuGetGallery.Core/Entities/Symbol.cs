// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.ComponentModel.DataAnnotations.Schema;

namespace NuGetGallery
{
    public class Symbol
        : IEntity
    {
        public Symbol()
        {
        }

        public int Key { get; set; }

        public Package Package { get; set; }

        public int PackageKey { get; set; }

        /// <summary>
        /// Timestamp when this symbol was created.
        /// </summary>
        [DatabaseGenerated(DatabaseGeneratedOption.Computed)]
        public DateTime Created { get; set; }

        /// <summary>
        /// Time when this symbol package is available for consumption. It will be updated after validations are complete.
        /// </summary>
        public DateTime? Published { get; set; }

        /// <summary>
        /// Hash value of the symbol package
        /// </summary>
        public string Hash { get; set; }

        /// <summary>
        /// Availability status of the symbol
        /// </summary>
        public PackageStatus StatusKey { get; set; }

        /// <summary>
        /// Used for optimistic concurrency when updating the symbols.
        /// </summary>
        public byte[] RowVersion { get; set; }
    }
}