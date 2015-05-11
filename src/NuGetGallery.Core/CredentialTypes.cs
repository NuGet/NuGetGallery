// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NuGetGallery
{
    public static class CredentialTypes
    {
        public static class Password
        {
            public static readonly string Prefix = "password.";
            public static readonly string Pbkdf2 = Prefix + "pbkdf2";
            public static readonly string Sha1 = Prefix + "sha1";
        }
        public static readonly string ApiKeyV1 = "apikey.v1";
        public static readonly string ExternalPrefix = "external.";


        public static bool IsPassword(string type)
        {
            return type.StartsWith(Password.Prefix, StringComparison.OrdinalIgnoreCase);
        }
    }
}
