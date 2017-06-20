// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

namespace NuGetGallery.Authentication.Providers
{
    public class AuthenticatorUI
    {
        public string SignInMessage { get; }
        public string RegisterMessage { get; }
        public string AccountNoun { get; }
        public string IconImagePath { get; set; }
        public string IconImageFallbackPath { get; set; }
        public bool ShowOnLoginPage { get; set; }

        public AuthenticatorUI(string signInMessage, string registerMessage, string accountNoun)
        {
            SignInMessage = signInMessage;
            RegisterMessage = registerMessage;
            AccountNoun = accountNoun;

            ShowOnLoginPage = true;
        }
    }
}
