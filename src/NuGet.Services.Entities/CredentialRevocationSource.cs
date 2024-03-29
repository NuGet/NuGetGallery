﻿// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

namespace NuGet.Services.Entities
{
    /// <summary>
    /// Specify the source about where the credential is revoked
    /// </summary>
    public enum CredentialRevocationSource
    {
        /// <summary>
        /// Indicate the credential is leaked and revoked by GitHub.
        /// </summary>
        GitHub = 0,

        /// <summary>
        /// Indicate the credential is revoked by User.
        /// </summary>
        User = 1,
    }
}