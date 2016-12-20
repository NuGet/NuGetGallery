// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Globalization;

namespace Stats.ImportAzureCdnStatistics
{
    public class PackageDimension
    {
        private static CultureInfo EnUsCulture = CultureInfo.GetCultureInfo("en-US");

        public PackageDimension(string packageId, string packageVersion)
        {
            PackageId = packageId;
            PackageVersion = packageVersion;
        }

        protected bool Equals(PackageDimension other)
        {
            return string.Equals(PackageId.ToLower(EnUsCulture), other.PackageId.ToLower(EnUsCulture)) && string.Equals(PackageVersion, other.PackageVersion, StringComparison.OrdinalIgnoreCase);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                return ((PackageId != null ? PackageId.ToLower(EnUsCulture).GetHashCode() : 0)*397) ^ (PackageVersion != null ? PackageVersion.GetHashCode() : 0);
            }
        }

        public int Id { get; set; }
        public string PackageId { get; }
        public string PackageVersion { get; }

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj)) return false;
            if (ReferenceEquals(this, obj)) return true;
            if (obj.GetType() != GetType()) return false;
            return Equals((PackageDimension) obj);
        }
    }
}