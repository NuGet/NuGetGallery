// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;

namespace NuGetGallery
{
    public class UserPackageDeleteEvent
    {
        public UserPackageDeleteEvent(
            TimeSpan sinceCreated,
            int packageKey,
            string packageId,
            string packageVersion,
            int idDatabaseDownloads,
            int idReportDownloads,
            int versionDatabaseDownloads,
            int versionReportDownloads,
            ReportPackageReason? reportPackageReason,
            PackageDeleteDecision? packageDeleteDecision)
        {
            SinceCreated = sinceCreated;
            PackageKey = packageKey;
            PackageId = packageId ?? throw new ArgumentNullException(nameof(packageId));
            PackageVersion = packageVersion ?? throw new ArgumentNullException(nameof(packageVersion));
            IdDatabaseDownloads = idDatabaseDownloads;
            IdReportDownloads = idReportDownloads;
            VersionDatabaseDownloads = versionDatabaseDownloads;
            VersionReportDownloads = versionReportDownloads;
            ReportPackageReason = reportPackageReason;
            PackageDeleteDecision = packageDeleteDecision;
        }

        public TimeSpan SinceCreated { get; }
        public int PackageKey { get; }
        public string PackageId { get; }
        public string PackageVersion { get; }
        public int IdDatabaseDownloads { get; }
        public int IdReportDownloads { get; }
        public int VersionDatabaseDownloads { get; }
        public int VersionReportDownloads { get; }
        public ReportPackageReason? ReportPackageReason { get; }
        public PackageDeleteDecision? PackageDeleteDecision { get; }
    }
}