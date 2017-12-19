// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Threading;
using System.Threading.Tasks;
using NuGet.Jobs.Validation.PackageSigning.Messages;
using NuGet.Packaging.Signing;
using NuGet.Services.Validation;

namespace NuGet.Jobs.Validation.PackageSigning.ExtractAndValidateSignature
{
    public interface ISignatureValidator
    {
        Task ValidateAsync(
            ISignedPackageReader signedPackageReader,
            ValidatorStatus validation,
            SignatureValidationMessage message,
            CancellationToken cancellationToken);
    }
}