// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;

namespace NuGet.Services.Metadata.Catalog.Dnx
{
    public sealed class DnxEntry
    {
        public Uri Nupkg { get; }
        public Uri Nuspec { get; }

        internal DnxEntry(Uri nupkg, Uri nuspec)
        {
            Nupkg = nupkg;
            Nuspec = nuspec;
        }
    }
}