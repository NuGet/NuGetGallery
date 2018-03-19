// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;

namespace NuGet.Jobs.Validation.PackageSigning.ProcessSignature
{
    /// <summary>
    /// All of the certificates extracted from a signature.
    /// </summary>
    public class ExtractedCertificates
    {
        public ExtractedCertificates(
            HashedCertificate signatureEndCertificate,
            IReadOnlyList<HashedCertificate> signatureParentCertificates,
            HashedCertificate timestampEndCertificate,
            IReadOnlyList<HashedCertificate> timestampParentCertificates)
        {
            SignatureEndCertificate = signatureEndCertificate ?? throw new ArgumentNullException(nameof(signatureEndCertificate));
            SignatureParentCertificates = signatureParentCertificates ?? throw new ArgumentNullException(nameof(signatureParentCertificates));
            TimestampEndCertificate = timestampEndCertificate ?? throw new ArgumentNullException(nameof(timestampEndCertificate));
            TimestampParentCertificates = timestampParentCertificates ?? throw new ArgumentNullException(nameof(timestampParentCertificates));
        }

        public HashedCertificate SignatureEndCertificate { get; }
        public IReadOnlyList<HashedCertificate> SignatureParentCertificates { get; }
        public HashedCertificate TimestampEndCertificate { get; }
        public IReadOnlyList<HashedCertificate> TimestampParentCertificates { get; }
    }
}
