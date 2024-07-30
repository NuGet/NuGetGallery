// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Threading.Tasks;
using NuGet.Services.Validation;

namespace Validation.PackageSigning.RevalidateCertificate
{
    public interface IValidateCertificateEnqueuer
    {
        Task EnqueueValidationAsync(Guid validationId, EndCertificate certificate);
    }
}
