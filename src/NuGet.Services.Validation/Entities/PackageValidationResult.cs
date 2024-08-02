// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;

namespace NuGet.Services.Validation
{
    /// <summary>
    /// A validation result provides additional information for a validation set,
    /// such as debugging information or reasons for failures. These results are
    /// produced by processing a validation set and are included the validation
    /// protocol's "validation response" message.
    /// </summary>
    public class PackageValidationResult
    {
        /// <summary>
        /// The unique key that identifies this result.
        /// </summary>
        public long Key { get; set; }

        /// <summary>
        /// The string that identifies this result's type, as per the validation protocol.
        /// For example, diagnostic results have a type of "Diagnostic". A validation set
        /// may have multiple results with the same type.
        /// </summary>
        public string Type { get; set; }

        /// <summary>
        /// A JSON object that contains additional information about the result,
        /// as per the validation protocol.
        /// </summary>
        public string Data { get; set; }

        /// <summary>
        /// The foreign key to the validation set that contains this result.
        /// </summary>
        /// <seealso cref="PackageValidationSet"/>
        public long PackageValidationSetKey { get; set; }

        /// <summary>
        /// The foreign key to the validation step that added this result.
        /// Null if the Orchestrator added this result.
        /// </summary>
        /// <seealso cref="PackageValidation"/>
        public Guid? PackageValidationKey { get; set; }

        /// <summary>
        /// The validation set that contains this result.
        /// </summary>
        public PackageValidationSet PackageValidationSet { get; set; }

        /// <summary>
        /// The validation step that added this result. Null if the Orchestrator added this result.
        /// </summary>
        public PackageValidation PackageValidation { get; set; }
    }
}
