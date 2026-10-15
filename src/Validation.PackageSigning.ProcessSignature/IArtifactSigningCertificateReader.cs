// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using NuGet.Packaging.Signing;

namespace NuGet.Jobs.Validation.PackageSigning.ProcessSignature
{
    public interface IArtifactSigningCertificateReader
    {
        /// <summary>
        /// Gets the durable identity value of the signing certificate if it was issued by Azure Artifact Signing.
        /// This does not check trust, revocation or validity; callers must only act on the result after the
        /// signature has passed full verification.
        /// </summary>
        bool TryGetDurableIdentityValue(PrimarySignature signature, out string durableIdentityValue);
    }
}
