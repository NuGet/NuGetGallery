// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Security.Claims;
using NuGetGallery.Authentication.Providers;

namespace NuGetGallery.Authentication
{
    public class AuthenticateExternalLoginResult
    {
        public AuthenticatedUser Authentication { get; set; }
        public ClaimsIdentity ExternalIdentity { get; set; }
        public Authenticator Authenticator { get; set; }
        public Credential Credential { get; set; }
    }
}