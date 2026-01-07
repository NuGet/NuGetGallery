// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;

namespace NuGet.Services.Validation
{
    /// <summary>
    /// An issue found by a <see cref="PackageValidation" /> that should be displayed to the user.
    /// </summary>
    public class PackageCompatibilityIssue
    {
        /// <summary>
        /// The unique key that identifies this error.
        /// </summary>
        public long Key { get; set; }

        /// <summary>
        /// The code that this error represents
        /// </summary>
        public string ClientIssueCode { get; set; }

        /// <summary>
        /// The error message's serialized data.
        /// </summary>
        public string Message { get; set; }

        /// <summary>
        /// The key to the <see cref="PackageValidation"/> that found this error.
        /// </summary>
        public Guid PackageValidationKey { get; set; }

        /// <summary>
        /// The <see cref="PackageValidation"/> that found this error.
        /// </summary>
        public PackageValidation PackageValidation { get; set; }
    }
}
