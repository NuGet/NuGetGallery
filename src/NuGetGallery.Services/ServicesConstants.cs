// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

namespace NuGetGallery
{
    public static class ServicesConstants
    {
        // X-NuGet-Client-Version header was deprecated and replaced with X-NuGet-Protocol-Version header
        // It stays here for backwards compatibility
        public const string ClientVersionHeaderName = "X-NuGet-Client-Version";
        public const string NuGetProtocolHeaderName = "X-NuGet-Protocol-Version";

        internal static readonly string UserAgentHeaderName = "User-Agent";
    }
}
