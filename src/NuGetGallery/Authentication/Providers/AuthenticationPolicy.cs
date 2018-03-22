// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;

namespace NuGetGallery.Authentication.Providers
{
    public class AuthenticationPolicy
    {
        public string Email { get; set; }

        public bool EnforceMfa { get; set; }

        private static string ENFORCE_MFA_TOKEN = "enforce_mfa";

        private static string EMAIL_TOKEN = "email";

        public IDictionary<string, string> GetProperties()
        {
            var dictionary = new Dictionary<string, string>();
            dictionary.Add(EMAIL_TOKEN, Email);
            dictionary.Add(ENFORCE_MFA_TOKEN, EnforceMfa.ToString());

            return dictionary;
        }

        public static bool TryGetPolicyFromProperties(IDictionary<string, string> properties, out AuthenticationPolicy policy)
        {
            if (properties != null
                && properties.TryGetValue(EMAIL_TOKEN, out string email)
                && properties.TryGetValue(ENFORCE_MFA_TOKEN, out string enforceMfaValue))
            {
                policy = new AuthenticationPolicy()
                {
                    Email = email,
                    EnforceMfa = Convert.ToBoolean(enforceMfaValue)
                };

                return true;
            }

            policy = null;
            return false;
        }

    }
}