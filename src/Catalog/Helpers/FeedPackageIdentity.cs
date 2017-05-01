// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;

namespace NuGet.Services.Metadata.Catalog.Helpers
{
    public class FeedPackageIdentity : IEquatable<FeedPackageIdentity>
    {
        public FeedPackageIdentity(string id, string version)
        {
            Id = id;
            Version = version;
        }

        public string Id { get; set; }
        public string Version { get; set; }

        public bool Equals(FeedPackageIdentity other)
        {
            return Id == other.Id && Version == other.Version;
        }

        public override int GetHashCode()
        {
            return Id.GetHashCode() ^ Version.GetHashCode();
        }
    }
}
