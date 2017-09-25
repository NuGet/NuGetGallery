// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using Newtonsoft.Json;
using NuGet.Packaging.Core;

namespace NuGet.Services.Metadata.Catalog.Helpers
{
    public class FeedPackageIdentity : IEquatable<FeedPackageIdentity>
    {
        [JsonProperty("id")]
        public string Id { get; private set; }

        [JsonProperty("version")]
        public string Version { get; private set; }

        [JsonConstructor]
        public FeedPackageIdentity(string id, string version)
        {
            Id = id;
            Version = version;
        }

        public FeedPackageIdentity(PackageIdentity package)
        {
            Id = package.Id;
            Version = package.Version.ToFullString();
        }

        public bool Equals(FeedPackageIdentity other)
        {
            return Id.ToLowerInvariant() == other.Id.ToLowerInvariant() && Version.ToLowerInvariant() == other.Version.ToLowerInvariant();
        }

        public override int GetHashCode()
        {
            return Tuple.Create(Id.ToLowerInvariant(), Version.ToLowerInvariant()).GetHashCode();
        }
    }
}