// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;

namespace NuGet.Services.Entities
{
    /// <summary>
    /// Provides the shared identity, status, and expiration deadline of a staged package or symbol attempt.
    /// </summary>
    public interface IStagedPackageAttempt
    {
        StagedPackageIdentity StagedPackageIdentity { get; }

        StagedPackageStatus Status { get; }

        DateTime ExpirationDate { get; }
    }
}
