// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using NuGet.Services.Metadata.Catalog.Helpers;
using NuGet.Versioning;
using System;

namespace NuGet.Services.Metadata.Catalog.Monitoring.Monitoring
{
    public class PackageStatusOutdatedCheck
    {
        public PackageStatusOutdatedCheck(
            FeedPackageIdentity identity,
            DateTime commitTimestamp)
        {
            Identity = identity;
            Timestamp = commitTimestamp;
        }

        public PackageStatusOutdatedCheck(
            FeedPackageDetails package)
            : this(
                  new FeedPackageIdentity(
                      package.PackageId,
                      ParseVersionString(package.PackageFullVersion)),
                  package.LastEditedDate)
        {
        }

        public PackageStatusOutdatedCheck(
            DeletionAuditEntry auditEntry)
            : this(
                  new FeedPackageIdentity(auditEntry.PackageId, ParseVersionString(auditEntry.PackageVersion)),
                  auditEntry.TimestampUtc.Value)
        {
        }

        public FeedPackageIdentity Identity { get; }
        public DateTime Timestamp { get; }

        private static string ParseVersionString(string version)
        {
            return NuGetVersion.TryParse(version, out var parsedVersion)
                ? parsedVersion.ToFullString() : version;
        }
    }
}
