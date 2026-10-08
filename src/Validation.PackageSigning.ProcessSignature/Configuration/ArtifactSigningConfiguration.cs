// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;

namespace NuGet.Jobs.Validation.PackageSigning.Configuration
{
    public class ArtifactSigningConfiguration
    {
        /// <summary>
        /// The SHA-256 fingerprint of "Microsoft ID Verified Code Signing PCA 2021", which is issued by
        /// "Microsoft Identity Verification Root Certificate Authority 2020".
        /// </summary>
        public const string DefaultPolicyCertificateThumbprint = "3D29798CC5D3F0644A7E0DC9CB1CADE523EA5EC83B335109B605BFEAA7D5F5C1";

        /// <summary>
        /// The EKU that Azure Artifact Signing adds to every certificate it issues.
        /// </summary>
        public string ArtifactSigningEkuOid { get; set; } = "1.3.6.1.4.1.311.97.1.0";

        /// <summary>
        /// The prefix of the EKU that carries the durable identity value of the validated identity.
        /// </summary>
        public string DurableIdentityValueOidPrefix { get; set; } = "1.3.6.1.4.1.311.97.";

        /// <summary>
        /// SHA-256 fingerprints of the CA certificates that an Artifact Signing certificate chain must contain.
        /// When null or empty, <see cref="DefaultPolicyCertificateThumbprint"/> is used. This is null by default so
        /// that configured values replace the default instead of being appended to it.
        /// </summary>
        public List<string> PolicyCertificateThumbprints { get; set; }

        /// <summary>
        /// A durable identity value is only linked to an account if the package signature was timestamped within
        /// this window before validation.
        /// </summary>
        public TimeSpan LinkWindow { get; set; } = TimeSpan.FromDays(30);

        public IReadOnlyList<string> GetPolicyCertificateThumbprints()
        {
            if (PolicyCertificateThumbprints == null || PolicyCertificateThumbprints.Count == 0)
            {
                return new[] { DefaultPolicyCertificateThumbprint };
            }

            return PolicyCertificateThumbprints;
        }
    }
}
