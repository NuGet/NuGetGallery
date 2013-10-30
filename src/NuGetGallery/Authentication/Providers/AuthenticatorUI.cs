using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Microsoft.Owin;

namespace NuGetGallery.Authentication.Providers
{
    public class AuthenticatorUI
    {
        public Uri LogoUrl { get; set; }
        public string SignInMessage { get; private set; }
        public string AccountNoun { get; private set; }

        public AuthenticatorUI(string signInMessage, string accountNoun)
        {
            SignInMessage = signInMessage;
            AccountNoun = accountNoun;
        }
    }
}
