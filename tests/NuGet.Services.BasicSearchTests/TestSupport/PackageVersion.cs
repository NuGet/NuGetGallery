// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;

namespace NuGet.Services.BasicSearchTests.TestSupport
{
    public class PackageVersion
    {
        public PackageVersion()
        {
        }

        public PackageVersion(string id, string version)
        {
            Id = id;
            Version = version;
        }
        
        public string Id { get; set; }
        
        public string Version { get; set; }

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