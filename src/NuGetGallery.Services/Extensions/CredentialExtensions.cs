// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using NuGet.Services.Entities;

namespace NuGetGallery
{
    /// <summary>
    /// APIs that provide lightweight extensibility for the Credential entity.
    /// </summary>
    public static class CredentialExtensions
    {
        /// <summary>
        /// Equality comparer for the Credential
        /// </summary>
        public static bool Matches(this Credential self, Credential cred)
        {
            return self.Type.Equals(cred.Type, StringComparison.OrdinalIgnoreCase)
                && self.Value == cred.Value;
        }
    }
}