// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;

namespace NuGet.Jobs.Validation.PackageSigning.Configuration
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

        /// <summary>
        /// If true, revalidating a package will strip its repository signature and then apply a new repository signature,
        /// even if current repository signature is valid. This mode should be disabled unless absolutely necessary!
        /// </summary>
        public bool StripValidRepositorySignatures { get; set; }

        /// <summary>
        /// Whether repository signatures should be persisted to the database. Disable this if repository signing
        /// is in test mode and repository signed packages are not published.
        /// </summary>
        public bool CommitRepositorySignatures { get; set; }

        /// <summary>
        /// The maximum length of a subject or issuer string to save to the gallery database. If a processed author
        /// certificate has an issuer or subject distinguished name or
        /// <see cref="System.Security.Cryptography.X509Certificates.X509NameType.SimpleName"/> that is longer than this
        /// value, null is stored in the database. We use 4000 since this is the maximum length for many package fields,
        /// such as description.
        /// </summary>
        public int MaxCertificateStringLength { get; set; } = 4000;
    }
}
