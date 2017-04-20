// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;

namespace NuGet.Indexing
{
    public class RegistrationBaseAddresses
    {
        public Uri LegacyHttp { get; set; }
        public Uri LegacyHttps { get; set; }
        public Uri SemVer2Http { get; set; }
        public Uri SemVer2Https { get; set; }
    }
}
