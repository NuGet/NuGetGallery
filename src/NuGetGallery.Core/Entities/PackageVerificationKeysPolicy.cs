// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;

namespace NuGetGallery
{
    public class PackageVerificationKeysPolicy
        : MinClientVersionPolicy
    {
        public static readonly Version PackageVerificationKeysClientVersion = new Version(4, 1, 0);

        public PackageVerificationKeysPolicy() : base(PackageVerificationKeysClientVersion)
        {
        }
    }
}
