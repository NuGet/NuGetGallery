// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;

namespace NuGetGallery
{
    /// <summary>
    /// Represents the application's request for validations to be performed on a package.
    /// </summary>
    public class PackageValidationSet
    {
        /// <summary>
        /// The database-mastered identifier for this validation set.
        /// </summary>
        public long Key { get; set; }

        /// <summary>
        /// A secondary key for the validation set. The application requesting the validation generates a GUID to
        /// identify a new set of validations. This value is unique as with the <see cref="Key"/>.
        /// </summary>
        public Guid ValidationTrackingId { get; set; }

        /// <summary>
        /// The foreign key referencing a <see cref="NuGetGallery.Package"/>.
        /// </summary>
        public int PackageKey { get; set; }

        /// <summary>
        /// Used for optimistic concurrency when updating which validations are in the set.
        /// </summary>
        public byte[] RowVersion { get; set; }

        /// <summary>
        /// The time when the validation set is created. This time should be shortly after the application requesting the
        /// validations generates the <see cref="ValidationTrackingId"/> as the creation of this record and the generation
        /// of the <see cref="ValidationTrackingId"/> are decoupled.
        /// </summary>
        public DateTime Created { get; set; }

        /// <summary>
        /// The time when the application last updated the package validation set. When the set is first created, the 
        /// <see cref="Created"/> timestamp shares the same value. When a validation is added to the validation set
        /// after the set is created, this timestamp is updated.
        /// </summary>
        public DateTime Updated { get; set; }
        
        /// <summary>
        /// The package that this validation set applies to.
        /// </summary>
        public Package Package { get; set; }
    }
}
