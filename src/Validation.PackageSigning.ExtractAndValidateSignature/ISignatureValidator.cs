// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Threading;
using System.Threading.Tasks;
using NuGet.Jobs.Validation.PackageSigning.Messages;
using NuGet.Packaging.Signing;

namespace NuGet.Jobs.Validation.PackageSigning.ExtractAndValidateSignature
{
    public interface ISignatureValidator
    {
        Task<SignatureValidatorResult> ValidateAsync(
            int packageKey,
            ISignedPackage signedPackage,
            SignatureValidationMessage message,
            CancellationToken cancellationToken);
    }
}