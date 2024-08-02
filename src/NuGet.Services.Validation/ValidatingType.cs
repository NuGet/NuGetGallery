// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

namespace NuGet.Services.Validation
{
    /// <summary>
    /// The type of the entity that will be validated.
    /// It will be persisted in the PackageValidationSet table.
    /// </summary>
    public enum ValidatingType
    {
        /// <summary>
        /// The entity is a NuGet package.
        /// </summary>
        Package = 0,

        /// <summary>
        /// The entity is a NuGet symbols package.
        /// </summary>
        SymbolPackage = 1,

        /// <summary>
        /// An entity whose validation was requested using the validation protocol.
        /// </summary>
        Generic = 2,
    }
}
