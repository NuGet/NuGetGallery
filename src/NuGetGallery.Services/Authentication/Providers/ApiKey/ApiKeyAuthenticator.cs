// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using NuGetGallery.Configuration;
using Owin;

namespace NuGetGallery.Authentication.Providers.ApiKey
{
    public class ApiKeyAuthenticator : Authenticator<ApiKeyAuthenticatorConfiguration>
    {
        protected override void AttachToOwinApp(IGalleryConfigurationService config, IAppBuilder app)
        {
            app.Map("/api", api =>
            {
                api.UseApiKeyAuthentication(new ApiKeyAuthenticationOptions
                {
                    ApiKeyHeaderName = Config.HeaderName,
                    ApiKeyClaim = Config.Claim
                });
            });
        }
    }
}