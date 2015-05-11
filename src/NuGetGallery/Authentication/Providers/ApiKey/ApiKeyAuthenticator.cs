// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.
using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using NuGetGallery.Configuration;
using Owin;

namespace NuGetGallery.Authentication.Providers.ApiKey
{
    public class ApiKeyAuthenticator : Authenticator<ApiKeyAuthenticatorConfiguration>
    {
        protected override void AttachToOwinApp(ConfigurationService config, IAppBuilder app)
        {
            app.UseApiKeyAuthentication(new ApiKeyAuthenticationOptions()
            {
                ApiKeyHeaderName = Config.HeaderName,
                ApiKeyClaim = Config.Claim
            });
        }
    }
}