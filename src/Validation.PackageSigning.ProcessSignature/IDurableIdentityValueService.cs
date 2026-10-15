// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Security.Cryptography.X509Certificates;
using System.Threading.Tasks;
using NuGet.Jobs.Validation.PackageSigning.Messages;
using NuGet.Services.Entities;

namespace NuGet.Jobs.Validation.PackageSigning.ProcessSignature
{
    public interface IDurableIdentityValueService
    {
        /// <summary>
        /// Records the durable identity value of a fully verified Artifact Signing certificate. It ensures the
        /// durable identity value and certificate records exist and are linked, and links the durable identity value
        /// to each signing account that has registered this certificate. This is idempotent.
        /// </summary>
        Task ProcessAsync(
            SignatureValidationMessage message,
            PackageRegistration packageRegistration,
            X509Certificate2 signingCertificate,
            string signingThumbprint,
            string durableIdentityValue,
            DateTimeOffset signatureTimestamp);
    }
}
