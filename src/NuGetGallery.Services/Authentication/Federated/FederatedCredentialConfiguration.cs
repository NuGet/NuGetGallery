// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

#nullable enable

using System;

namespace NuGetGallery.Services.Authentication
{
    public interface IFederatedCredentialConfiguration
    {
        /// <summary>
        /// The expected audience for the incoming token. This is the "aud" claim and should be specific to the gallery
        /// service itself (not shared between multiple services). This is used only for Entra ID token validation.
        /// </summary>
        string? EntraIdAudience { get; }

        /// <summary>
        /// How long the short lived API keys should last.
        /// </summary>
        TimeSpan ShortLivedApiKeyDuration { get; }
    }

    public class FederatedCredentialConfiguration : IFederatedCredentialConfiguration
    {
        public string? EntraIdAudience { get; set; }

        public TimeSpan ShortLivedApiKeyDuration { get; set; } = TimeSpan.FromMinutes(15);
    }
}
