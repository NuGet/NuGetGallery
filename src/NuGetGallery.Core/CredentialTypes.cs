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
    }
}
