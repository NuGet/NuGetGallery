// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;

namespace NuGet.Jobs.Validation.PackageSigning.Messages
{
    public class SignatureValidationMessage
    {
        public SignatureValidationMessage(
            string packageId,
            string packageVersion,
            Uri nupkgUri,
            Guid validationId,
            bool requireRepositorySignature = false)
        {
            if (validationId == Guid.Empty)
            {
                throw new ArgumentOutOfRangeException(nameof(validationId));
            }

            PackageId = packageId ?? throw new ArgumentNullException(nameof(packageId));
            PackageVersion = packageVersion ?? throw new ArgumentNullException(nameof(packageVersion));
            NupkgUri = nupkgUri ?? throw new ArgumentNullException(nameof(nupkgUri));
            ValidationId = validationId;
            RequireRepositorySignature = requireRepositorySignature;
        }

        public string PackageId { get; }
        public string PackageVersion { get; }
        public Uri NupkgUri { get; }
        public Guid ValidationId { get; }
        public bool RequireRepositorySignature { get; }
    }
}