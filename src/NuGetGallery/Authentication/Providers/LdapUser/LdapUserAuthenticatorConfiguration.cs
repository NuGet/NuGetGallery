// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

namespace NuGetGallery.Authentication.Providers.LdapUser
{
    public class LdapUserAuthenticatorConfiguration : AuthenticatorConfiguration
    {
        public string Host { get; set; }
        public string Port { get; set; }
        public string ServiceAccountUserName { get; set; }
        public string ServiceAccountPassword { get; set; }
        public string UserBase { get; set; }
        public string ObjectFilter { get; set; }
        public string NameAttribute { get; set; }
        public string GroupAttribute { get; set; }
        public string AllowedGroup { get; set; }

        public static readonly string DefaultAuthenticationType = "LdapUser";

        public LdapUserAuthenticatorConfiguration()
        {
            AuthenticationType = DefaultAuthenticationType;
        }
    }
}
