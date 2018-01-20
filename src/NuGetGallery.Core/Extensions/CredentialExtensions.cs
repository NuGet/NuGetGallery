// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;

namespace NuGetGallery
{
    public static class CredentialExtensions
    {
        public static bool IsPassword(this Credential c)
        {
            return c.IsType(CredentialTypes.Password.Prefix);
        }

        public static bool IsExternal(this Credential c)
        {
            return c.Type.StartsWith(CredentialTypes.ExternalPrefix, StringComparison.OrdinalIgnoreCase);
        }

        public static bool IsExternal(this Credential c, string provider)
        {
            return c.IsType(CredentialTypes.ExternalPrefix + provider);
        }

        public static bool IsType(this Credential c, string type)
        {
            return c.Type.Equals(type, StringComparison.OrdinalIgnoreCase);
        }
    }
}
