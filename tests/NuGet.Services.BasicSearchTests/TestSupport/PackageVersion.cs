// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;

namespace NuGet.Services.BasicSearchTests.TestSupport
{
    public class PackageVersion
    {
        public PackageVersion(string id, string version, int downloads = 0, bool listed = true)
        {
            Id = id;
            Version = version;
            Downloads = downloads;
            Listed = listed;
        }

        public string Id { get; }

        public string Version { get; }

        public int Downloads { get; }

        public bool Listed { get; }

        public override bool Equals(object obj)
        {
            if (!(obj is PackageVersion))
            {
                return false;
            }

            var other = (PackageVersion)obj;

            return
                Id.Equals(other.Id, StringComparison.OrdinalIgnoreCase) &&
                Version.Equals(other.Version, StringComparison.OrdinalIgnoreCase);
        }

        public override string ToString()
        {
            return
                "{" +
                $"Id: {Id}, " +
                $"Version: {Version}" +
                "}";
        }

        public override int GetHashCode()
        {
            return ToString().ToLower().GetHashCode();
        }
    }
}