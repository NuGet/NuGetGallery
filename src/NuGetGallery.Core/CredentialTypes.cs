// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;

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

        public const string ApiKeyV1 = "apikey.v1";
        public const string ExternalPrefix = "external.";

        public static bool IsPassword(string type)
        {
            return type.StartsWith(Password.Prefix, StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// Forward compatibility - we support only the bellow subset of credentials in this code version.
        /// Any unrecognized credential will be ignored.
        /// </summary>
        /// <param name="credential"></param>
        /// <returns></returns>
        public static bool IsSupportedCredential(Credential credential)
        {
            return string.Compare(credential.Type, Password.Pbkdf2, StringComparison.OrdinalIgnoreCase) == 0 ||
                   string.Compare(credential.Type, Password.Sha1, StringComparison.OrdinalIgnoreCase) == 0 ||
                   string.Compare(credential.Type, Password.V3, StringComparison.OrdinalIgnoreCase) == 0 ||
                   string.Compare(credential.Type, ApiKeyV1, StringComparison.OrdinalIgnoreCase) == 0 ||
                   credential.Type.StartsWith(ExternalPrefix, StringComparison.OrdinalIgnoreCase);
        }
    }
}
