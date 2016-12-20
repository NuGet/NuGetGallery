// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.
using System.Web.Mvc;
using NuGetGallery.Configuration;
using Microsoft.Owin.Security.RPS;
using Owin;

namespace NuGetGallery.Authentication.Providers.RPSMicrosoftAccount
{
    public class RPSMicrosoftAccountAuthenticator : Authenticator<RPSMicrosoftAccountAuthenticatorConfiguration>
    {
        public static readonly string DefaultAuthenticationType = "RPSMicrosoftAccount";

        protected override void AttachToOwinApp(IGalleryConfigurationService config, IAppBuilder app)
        {
            var options = new RPSAuthenticationOptions();
            Config.ApplyToOwinSecurityOptions(options);
            app.UseRPSAuthentication(options);
        }

        public override AuthenticatorUI GetUI()
        {
            return new AuthenticatorUI(
                "Sign in with a RPS Microsoft account",
                Strings.MicrosoftAccount_AccountNoun,
                Strings.MicrosoftAccount_Caption)
                {
                    IconCssClass = "icon-windows"
                };
        }

        public override ActionResult Challenge(string redirectUrl)
        {
            //TODO : Need a better way to get full url
            redirectUrl = "https://nuget.localtest.me" + redirectUrl;
            return new ChallengeResult(BaseConfig.AuthenticationType, redirectUrl);
        }
    }
}