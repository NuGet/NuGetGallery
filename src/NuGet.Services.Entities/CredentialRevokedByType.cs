// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

namespace NuGet.Services.Entities
{
    /// <summary>
    /// Specify the type about where the ApiKey is leaked
    /// </summary>
    public enum CredentialRevokedByType
    {
        /// <summary>
        /// Indicate the ApiKey is revoked because of the leaking from GitHub.
        /// </summary>
        GitHub = 0,
    }
}