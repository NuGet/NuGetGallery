// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

namespace NuGet.Services.Validation
{
    /// <summary>
    /// The type of a <see cref="PackageSignature"/>.
    /// </summary>
    public enum PackageSignatureType
    {
        /// <summary>
        /// An author signature. If present, this is always the primary signature.
        /// </summary>
        Author = 1,

        /// <summary>
        /// A repository signature. If an <see cref="Author"/> signature is present on the same package, this signature
        /// is a counter signature. Otherwise, it is the primary signature.
        /// </summary>
        Repository = 2,
    }
}
