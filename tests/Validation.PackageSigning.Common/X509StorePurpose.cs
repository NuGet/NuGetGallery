// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

namespace Microsoft.Internal.NuGet.Testing.SignedPackages
{
    // Only exists so as to avoid requiring NuGet.Packaging.Signing.X509StorePurpose from being public.
    public enum X509StorePurpose
    {
        CodeSigning = 1,
        Timestamping = 2
    }
}
