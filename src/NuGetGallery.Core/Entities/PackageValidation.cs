// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;

namespace NuGetGallery
{
    /// <summary>
    /// Represents a single validation performed on a package. The associated package is implied via the validations's
    /// <see cref="NuGetGallery.PackageValidationSet"/>.
    /// </summary>
    public class PackageValidation
    {
        /// <summary>
        /// The database-mastered identifier for this validation.
        /// </summary>
        public Guid Key { get; set; }

        /// <summary>
        /// The foreign key referencing a <see cref="NuGetGallery.PackageValidationSet"/>.
        /// </summary>
        public long PackageValidationSetKey { get; set; }

        /// <summary>
        /// The human-readable name of the validation that this record represents. When a validation set is first
        /// created, the type should be unique within the set. However, if individual validations are re-attempted
        /// there could be multiple validations within a set with the same type (but different <see cref="Key"/>).
        /// This value has a maximum length of 255 characters.
        /// </summary>
        public string Type { get; set; }

        /// <summary>
        /// The status of this validation.
        /// </summary>
        public ValidationStatus ValidationStatusKey { get; set; }

        /// <summary>
        /// The time when the current validation status was set.
        /// </summary>
        public DateTime ValidationStatusTimestamp { get; set; }

        /// <summary>
        /// The set that this validation is part of.
        /// </summary>
        public PackageValidationSet PackageValidationSet { get; set; }
    }
}
