// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

namespace NuGetGallery.Authentication.Providers
{
    public class AuthenticatorUI
    {
        public string SignInMessage { get; private set; }
        public string AccountNoun { get; private set; }
        public string Caption { get; private set; }
        public string IconImagePath { get; set; }
        public string IconCssClass { get; set; }
        public bool ShowOnLoginPage { get; set; }
        public string SignInInfo { get; set; }

        public AuthenticatorUI(string signInMessage, string accountNoun, string caption)
        {
            SignInMessage = signInMessage;
            AccountNoun = accountNoun;
            Caption = caption;

            ShowOnLoginPage = true;
        }
    }
}
