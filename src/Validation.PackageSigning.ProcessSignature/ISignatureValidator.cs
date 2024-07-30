// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.IO;
using System.Threading;
using System.Threading.Tasks;
using NuGet.Jobs.Validation.PackageSigning.Messages;

namespace NuGet.Jobs.Validation.PackageSigning.ProcessSignature
{
    public interface ISignatureValidator
    {
        Task<SignatureValidatorResult> ValidateAsync(
            int packageKey,
            Stream packageStream,
            SignatureValidationMessage message,
            CancellationToken cancellationToken);
    }
}