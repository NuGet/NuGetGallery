// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Web;

namespace NuGetGallery.Authentication.Providers.ApiKey
{
    public class ApiKeyAuthenticatorConfiguration : AuthenticatorConfiguration
    {
        public ApiKeyAuthenticatorConfiguration()
        {
            AuthenticationType = AuthenticationTypes.ApiKey;
        }

        [DefaultValue(Constants.ApiKeyHeaderName)]
        public string HeaderName { get; set; }

        [DefaultValue(NuGetClaims.ApiKey)]
        public string Claim { get; set; }
    }
}