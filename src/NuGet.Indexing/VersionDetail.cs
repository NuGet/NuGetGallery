// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Diagnostics;

namespace NuGet.Indexing
{
    [DebuggerDisplay("v: {Version} d: {Downloads} l: {IsListed}")]
    public class VersionDetail
    {
        public string NormalizedVersion { get; set; }
        public string FullVersion { get; set; }
        public int Downloads { get; set; }
        public bool IsStable { get; set; }
        public bool IsListed { get; set; }
        public bool IsSemVer2 { get; set; }
    }
}
