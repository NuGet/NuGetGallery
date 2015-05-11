// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.
using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using Microsoft.Owin.Security;

namespace NuGetGallery.Authentication.Providers.ApiKey
{
    public class ApiKeyAuthenticationOptions : AuthenticationOptions
    {
        public string ApiKeyHeaderName { get; set; }
        public string ApiKeyClaim { get; set; }
        
        public ApiKeyAuthenticationOptions() : base(AuthenticationTypes.ApiKey) {
            ApiKeyHeaderName = Constants.ApiKeyHeaderName;
            ApiKeyClaim = NuGetClaims.ApiKey;
        }
    }
}