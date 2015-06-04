// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

namespace NuGet.Indexing
{
    public class VersionDownloads
    {
        public VersionDownloads(string normalizedVersion, int downloads)
        {
            NormalizedVersion = normalizedVersion;
            Downloads = downloads;
        }

        public string NormalizedVersion { get; private set; }
        public int Downloads { get; private set; }
    }
}