// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;

namespace NuGet.Services.Validation
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
        /// The key referencing a package in the NuGet Gallery database. If a package is hard deleted then re-pushed,
        /// the <see cref="PackageId"/> and <see cref="PackageNormalizedVersion"/> version will be the same but the
        /// <see cref="PackageKey"/> will be different.
        /// </summary>
        public int PackageKey { get; set; }

        /// <summary>
        /// The package ID. Has a maximum length of 128 unicode characters as defined by the NuGet Gallery database.
        /// </summary>
        public string PackageId { get; set; }

        /// <summary>
        /// The normalized package version. Has a maximum length of 64 unicode characters as defined by the NuGet
        /// Gallery database.
        /// </summary>
        public string PackageNormalizedVersion { get; set; }

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
        /// The etag of the available package being validated. Null if the package is not yet available (i.e. it is not
        /// yet in the packages container). A validation set for a package that is already available will have its
        /// packages container package etag in this column. This is used to control the concurrency of validation sets
        /// that mutate packages.
        /// </summary>
        public string PackageETag { get; set; }

        /// <summary>
        /// Used for optimistic concurrency when updating which validations are in the set.
        /// </summary>
        public byte[] RowVersion { get; set; }

        /// <summary>
        /// The package validations in the validation set.
        /// </summary>
        public virtual ICollection<PackageValidation> PackageValidations { get; set; }

        /// <summary>
        /// The entity type to be validated.
        /// </summary>
        public ValidatingType ValidatingType { get; set; }

        /// <summary>
        /// The status of this validation set.
        /// </summary>
        public ValidationSetStatus ValidationSetStatus { get; set; }
    }
}
