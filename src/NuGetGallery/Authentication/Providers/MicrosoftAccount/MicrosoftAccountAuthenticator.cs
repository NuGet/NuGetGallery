// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Security.Claims;
using System.Web.Mvc;
using Microsoft.Owin.Security.MicrosoftAccount;
using NuGetGallery.Authentication.Providers.Utils;
using NuGetGallery.Configuration;
using Owin;

namespace NuGetGallery.Authentication.Providers.MicrosoftAccount
{
    public class MicrosoftAccountAuthenticator : Authenticator<MicrosoftAccountAuthenticatorConfiguration>
    {
        public static readonly string DefaultAuthenticationType = "MicrosoftAccount";

        protected override void AttachToOwinApp(IGalleryConfigurationService config, IAppBuilder app)
        {
            var options = new MicrosoftAccountAuthenticationOptions();
            options.Scope.Add("wl.emails");
            options.Scope.Add("wl.signin");
            Config.ApplyToOwinSecurityOptions(options);
            app.UseMicrosoftAccountAuthentication(options);
        }

        public override AuthenticatorUI GetUI()
        {
            return new AuthenticatorUI(
                Strings.MicrosoftAccount_SignInMessage,
                Strings.MicrosoftAccount_SignInMessage,
                Strings.MicrosoftAccount_AccountNoun)
            {
                IconImagePath = "~/Content/gallery/img/microsoft-account.svg",
                IconImageFallbackPath = "~/Content/gallery/img/microsoft-account-24x24.png",
            };
        }

        public override ActionResult Challenge(string redirectUrl)
        {
            return new ChallengeResult(BaseConfig.AuthenticationType, redirectUrl);
        }

        public override IdentityInformation GetIdentityInformation(ClaimsIdentity claimsIdentity)
        {
            return ClaimsExtentions.GetIdentityInformation(claimsIdentity, DefaultAuthenticationType);
        }
    }
}