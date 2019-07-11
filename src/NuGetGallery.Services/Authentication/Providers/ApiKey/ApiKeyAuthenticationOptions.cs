// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.
using Microsoft.Owin.Security;

namespace NuGetGallery.Authentication.Providers.ApiKey
{
    public class ApiKeyAuthenticationOptions : AuthenticationOptions
    {
        public string ApiKeyHeaderName { get; set; }
        public string ApiKeyClaim { get; set; }
        
        public ApiKeyAuthenticationOptions() : base(AuthenticationTypes.ApiKey) {
            ApiKeyHeaderName = ServicesConstants.ApiKeyHeaderName;
            ApiKeyClaim = NuGetClaims.ApiKey;
        }
    }
}