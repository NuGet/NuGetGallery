// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Globalization;

namespace Stats.ImportAzureCdnStatistics
{
    public class PackageDimension : IEquatable<PackageDimension>
    {
        private static CultureInfo EnUsCulture = CultureInfo.GetCultureInfo("en-US");

        private readonly string _comparablePackageId;
        private readonly int _hashCode;

        public PackageDimension(string packageId, string packageVersion)
        {
            PackageId = packageId;
            PackageVersion = packageVersion;
            _comparablePackageId = packageId.ToLower(EnUsCulture);

            unchecked
            {
                _hashCode = ((PackageId != null ? _comparablePackageId.GetHashCode() : 0) * 397) ^ (PackageVersion != null ? PackageVersion.GetHashCode() : 0);
            }
        }

        public bool Equals(PackageDimension other)
        {
            if (other == null) return false;
            return string.Equals(_comparablePackageId, other._comparablePackageId) && string.Equals(PackageVersion, other.PackageVersion, StringComparison.OrdinalIgnoreCase);
        }

        public override int GetHashCode() => _hashCode;

        public int Id { get; set; }
        public string PackageId { get; }
        public string PackageVersion { get; }

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(this, obj)) return true;
            var other = obj as PackageDimension;
            if (other == null) return false;
            return Equals(other);
        }
    }
}