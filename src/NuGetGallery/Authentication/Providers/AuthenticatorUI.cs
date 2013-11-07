using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Microsoft.Owin;

namespace NuGetGallery.Authentication.Providers
{
    public class AuthenticatorUI
    {
        public string SignInMessage { get; private set; }
        public string AccountNoun { get; private set; }
        public string Caption { get; private set; }
        public string IconCssClass { get; set; }

        public AuthenticatorUI(string signInMessage, string accountNoun, string caption)
        {
            SignInMessage = signInMessage;
            AccountNoun = accountNoun;
            Caption = caption;
        }
    }
}
