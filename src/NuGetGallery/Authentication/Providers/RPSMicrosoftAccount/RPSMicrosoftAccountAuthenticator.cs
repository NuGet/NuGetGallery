// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.
using System.Web.Mvc;
using Microsoft.Owin.Security.MicrosoftAccount;
using NuGetGallery.Configuration;
using Owin;

namespace NuGetGallery.Authentication.Providers.RPSMicrosoftAccount
{
    public class RPSMicrosoftAccountAuthenticator : Authenticator<RPSMicrosoftAccountAuthenticatorConfiguration>
    {
        public static readonly string DefaultAuthenticationType = "RPSMicrosoftAccount";

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
                Strings.MicrosoftAccount_AccountNoun,
                Strings.MicrosoftAccount_Caption)
                {
                    IconCssClass = "icon-windows"
                };
        }

        public override ActionResult Challenge(string redirectUrl)
        {
            return new ChallengeResult(BaseConfig.AuthenticationType, redirectUrl);
        }
    }
}