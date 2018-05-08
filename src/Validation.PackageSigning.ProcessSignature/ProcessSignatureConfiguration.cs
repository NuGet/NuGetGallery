// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;

namespace NuGet.Jobs.Validation.PackageSigning
{
    public class ProcessSignatureConfiguration
    {
        /// <summary>
        /// When a package with a repository signature is being validated, the signing certificate of the repository
        /// signature must have a SHA-256 fingerprint in this list. If it does not, the repository signature is
        /// removed.
        /// </summary>
        public List<string> AllowedRepositorySigningCertificates { get; set; }

        /// <summary>
        /// The service index URL to validate against any repository signature. If a package being validated has a
        /// repository signature and that signature has a V3 service index URL that does not match this value, the
        /// repository signature is removed.
        /// </summary>
        public string V3ServiceIndexUrl { get; set; }
    }
}
