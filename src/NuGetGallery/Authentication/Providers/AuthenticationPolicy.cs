// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;

namespace NuGetGallery.Authentication.Providers
{
    public class AuthenticationPolicy
    {
        public string Email { get; set; }

        public bool EnforceMultiFactorAuthentication { get; set; }

        private static string _enforceMutliFactorAuthenticationToken = "enforce_mfa";
        private static string _emailToken = "email";

        public IDictionary<string, string> GetProperties()
        {
            var dictionary = new Dictionary<string, string>();
            dictionary.Add(_emailToken, Email);
            dictionary.Add(_enforceMutliFactorAuthenticationToken, EnforceMultiFactorAuthentication.ToString());

            return dictionary;
        }

        public static bool TryGetPolicyFromProperties(IDictionary<string, string> properties, out AuthenticationPolicy policy)
        {
            if (properties != null
                && properties.TryGetValue(_emailToken, out string email)
                && properties.TryGetValue(_enforceMutliFactorAuthenticationToken, out string enforceMfaValue))
            {
                policy = new AuthenticationPolicy()
                {
                    Email = email,
                    EnforceMultiFactorAuthentication = Convert.ToBoolean(enforceMfaValue)
                };

                return true;
            }

            policy = null;
            return false;
        }

    }
}