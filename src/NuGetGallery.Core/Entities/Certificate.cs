// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

namespace NuGetGallery
{
    public class Certificate : IEntity
    {
        /// <summary>
        /// The database-mastered identifier for this certificate.
        /// </summary>
        public int Key { get; set; }

        /// <summary>
        /// The SHA-256 thumbprint (fingerprint) that uniquely identifies this certificate. This is a string with
        /// exactly 64 characters and is the hexadecimal encoding of the hash digest. Note that the SQL column that
        /// stores this property allows longer string values to facilitate future hash algorithm changes.
        /// </summary>
        public string Thumbprint { get; set; }
    }
}
