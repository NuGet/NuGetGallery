// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

#nullable enable

using System;
using System.ComponentModel;
using NuGet.Services.Configuration;
using NuGet.Services.Entities;

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

        /// <summary>
        /// The list of all Entra ID tenant GUIDs that are allowed for <see cref="FederatedCredentialType.EntraIdServicePrincipal"/>
        /// federated credential policies. If this list is empty, no tenants are allowed. If this list has a single
        /// item "all", then all Entra ID tenants are allowed.
        ///
        /// Values are separated by a semicolon when provided in the configuration file.
        /// </summary>
        string[] AllowedEntraIdTenants { get; }
    }

    public class FederatedCredentialConfiguration : IFederatedCredentialConfiguration
    {
        public string? EntraIdAudience { get; set; }

        public TimeSpan ShortLivedApiKeyDuration { get; set; } = TimeSpan.FromMinutes(15);

        [TypeConverter(typeof(StringArrayConverter))]
        public string[] AllowedEntraIdTenants { get; set; } = [];
    }
}
