// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;

namespace NuGetGallery
{
    public static class CredentialTypes
    {
        public static class Password
        {
            public const string Prefix = "password.";
            public const string Pbkdf2 = Prefix + "pbkdf2";
            public const string Sha1 = Prefix + "sha1";
            public const string V3 = Prefix + "v3";
        }

        public static class ApiKey
        {
            public const string Prefix = "apikey.";
            public const string V1 = Prefix + "v1";
            public const string V2 = Prefix + "v2";
            public const string V3 = Prefix + "v3";
            public const string V4 = Prefix + "v4";
            public const string VerifyV1 = Prefix + "verify.v1";
        }

        public const string ExternalPrefix = "external.";

        public static bool IsPassword(string type)
        {
            if (type == null)
            {
                throw new ArgumentNullException(nameof(type));
            }

            return type.StartsWith(Password.Prefix, StringComparison.OrdinalIgnoreCase);
        }

        public static bool IsApiKey(string type)
        {
            return type.StartsWith(ApiKey.Prefix, StringComparison.OrdinalIgnoreCase);
        }

        public static bool IsPackageVerificationApiKey(string type)
        {
            return type.Equals(ApiKey.VerifyV1, StringComparison.OrdinalIgnoreCase);
        }
        
        internal static IReadOnlyList<string> SupportedCredentialTypes = new List<string>
            {
                Password.Sha1,
                Password.Pbkdf2,
                Password.V3,
                ApiKey.V1,
                ApiKey.V2,
                ApiKey.V3,
                ApiKey.V4
            };

        /// <summary>
        /// Determines whether a credential is supported (internal or from the UI). For forward compatibility,
        /// this version supports only the credentials below and ignores any others.
        /// </summary>
        /// <param name="credential"></param>
        /// <returns></returns>
        public static bool IsSupportedCredential(this Credential credential)
        {
            return credential.IsViewSupportedCredential() || IsPackageVerificationApiKey(credential.Type);
        }

        /// <summary>
        /// Determines whether a credential is supported from the user interface. For forward compatibility,
        /// this version supports only the credentials below and ignores any others.
        /// </summary>
        /// <param name="credential"></param>
        /// <returns></returns>
        public static bool IsViewSupportedCredential(this Credential credential)
        {
            return SupportedCredentialTypes.Any(credType => string.Compare(credential.Type, credType, StringComparison.OrdinalIgnoreCase) == 0)
                    || credential.Type.StartsWith(ExternalPrefix, StringComparison.OrdinalIgnoreCase);
        }

        public static bool IsScopedApiKey(this Credential credential)
        {
            return IsApiKey(credential.Type) && credential.Scopes != null && credential.Scopes.Any();
        }
    }
}
