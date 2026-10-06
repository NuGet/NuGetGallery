// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Linq;

namespace NuGetGallery
{
    internal static class NamespaceReservationSqlQueries
    {
        // SQL Server / LocalDB SELECT syntax, without a dependency on FOR JSON or OPENJSON
        // compatibility levels. Table/column mappings are from EntitiesContext and migrations
        // Initial, PrefixReservation, Organizations, SecurityPoliciesFix, SubscriptionColumn,
        // AddAccountDelete, AddUserStatusKeyColumn and AddPackageStatusKey.
        // Only these fixed, application-owned statements reach DbCommand. Slot names, not
        // request values, are composed here. Every value is bound with an explicit DbType.
        internal static readonly string OwnerScope = @"WITH RequestedOwners AS (
    SELECT OwnerKey FROM (VALUES " + string.Join(",", Enumerable.Range(0, NamespaceReservationSqlEvidenceSession.MaxOwnerCount)
            .Select(i => "(@owner" + i + ")")) + @") AS o(OwnerKey) WHERE OwnerKey IS NOT NULL
)";

        internal static readonly string Accounts = OwnerScope + @",
RequestedAccounts AS (
    SELECT @submitter AS AccountKey, 0 AS Ordinal
    UNION ALL
    SELECT OwnerKey, ROW_NUMBER() OVER (ORDER BY OwnerKey) FROM RequestedOwners
)
SELECT TOP (20) a.Ordinal, u.Username, u.EmailAddress, u.IsDeleted, u.UserStatusKey,
    CAST(CASE WHEN u.[Key] IS NULL THEN 0 ELSE 1 END AS bit) AS AccountExists,
    CAST(CASE WHEN o.[Key] IS NULL THEN 0 ELSE 1 END AS bit) AS IsOrganization,
    CAST(CASE WHEN m.IsAdmin = 1 THEN 1 ELSE 0 END AS bit) AS AdminMembership,
    CAST(CASE WHEN a.AccountKey = @submitter THEN 1 ELSE 0 END AS bit) AS IsSubmitter,
    CAST(CASE WHEN EXISTS (SELECT 1 FROM dbo.UserSecurityPolicies sp
        WHERE sp.UserKey = u.[Key] AND sp.Subscription = @subscription)
        THEN 1 ELSE 0 END AS bit) AS MicrosoftPolicySubscriptionObserved
FROM RequestedAccounts a
LEFT JOIN dbo.Users u ON u.[Key] = a.AccountKey
LEFT JOIN dbo.Organizations o ON o.[Key] = u.[Key]
LEFT JOIN dbo.Memberships m ON m.OrganizationKey = o.[Key] AND m.MemberKey = @submitter
ORDER BY a.Ordinal";

        internal static readonly string Reservations = OwnerScope + @",
Matches AS (
    SELECT rn.Value AS namespaceValue, rn.IsPrefix AS isPrefix,
        rn.IsSharedNamespace AS isSharedNamespace,
           CASE WHEN rn.Value COLLATE Latin1_General_100_CI_AS = @namespace THEN 'exact'
               WHEN rn.IsPrefix = 1 AND LEFT(@namespace, LEN(rn.Value)) COLLATE Latin1_General_100_CI_AS = rn.Value THEN 'parent'
             ELSE 'descendant' END AS relationship,
        (SELECT COUNT_BIG(*) FROM dbo.ReservedNamespaceOwners ro
            WHERE ro.ReservedNamespaceKey = rn.[Key]) AS ownerCount,
        (SELECT COUNT_BIG(*) FROM dbo.ReservedNamespaceOwners ro
            WHERE ro.ReservedNamespaceKey = rn.[Key]
            AND ro.UserKey IN (SELECT OwnerKey FROM RequestedOwners)) AS requestedOwnerCount
    FROM dbo.ReservedNamespaces rn
     WHERE rn.Value COLLATE Latin1_General_100_CI_AS = @namespace
         OR (rn.IsPrefix = 1 AND LEFT(@namespace, LEN(rn.Value)) COLLATE Latin1_General_100_CI_AS = rn.Value)
         OR rn.Value COLLATE Latin1_General_100_CI_AS LIKE @rawPrefix ESCAPE '~'
), Summary AS (
    SELECT COUNT_BIG(*) AS overlapCount,
        COALESCE(SUM(CONVERT(bigint, CASE WHEN relationship = 'exact' THEN 1 ELSE 0 END)), 0) AS exactCount,
        COALESCE(SUM(CONVERT(bigint, CASE WHEN relationship = 'parent' THEN 1 ELSE 0 END)), 0) AS parentCount,
        COALESCE(SUM(CONVERT(bigint, CASE WHEN relationship = 'descendant' THEN 1 ELSE 0 END)), 0) AS descendantCount,
        COALESCE(SUM(CONVERT(bigint, CASE WHEN isSharedNamespace = 1 THEN 1 ELSE 0 END)), 0) AS sharedCount
    FROM Matches
)
SELECT s.overlapCount, s.exactCount, s.parentCount, s.descendantCount, s.sharedCount,
    d.namespaceValue, d.isPrefix, d.isSharedNamespace, d.relationship, d.ownerCount, d.requestedOwnerCount
FROM Summary s
OUTER APPLY (SELECT TOP (20) namespaceValue, isPrefix, isSharedNamespace, relationship, ownerCount, requestedOwnerCount
    FROM Matches ORDER BY namespaceValue, isPrefix) d
ORDER BY d.namespaceValue, d.isPrefix";

        internal static readonly string[] ReservationCounts =
        {
            "overlapCount", "exactCount", "parentCount", "descendantCount", "sharedCount"
        };

        internal static readonly string[] ReservationFields =
        {
            "namespaceValue", "isPrefix", "isSharedNamespace", "relationship", "ownerCount", "requestedOwnerCount"
        };

        internal static readonly string[] VersionCounts =
        {
            "versionCount", "availableVersionCount", "deletedVersionCount", "validatingVersionCount",
            "failedValidationVersionCount", "unknownStatusVersionCount", "listedAvailableVersionCount",
            "createdSinceCutoffVersionCount", "publishedSinceCutoffAvailableVersionCount", "anomalousTimestampVersionCount"
        };

        internal static readonly string[] DateFields =
        {
            "earliestStoredCreatedUtc", "latestStoredCreatedUtc", "earliestStoredPublishedUtc", "latestStoredPublishedUtc"
        };

        internal static readonly string[] PackageFields = new[] { "packageId", "ownerCount", "requestedOwnerCount" }
            .Concat(VersionCounts).Concat(DateFields).ToArray();

        internal static readonly string[] UsageCounts = new[]
        {
            "packageCount", "requestedOwnerCount", "requestedOwnerWithPublishedPackageCount",
            "requestedOwnerPackageCount", "requestedOwnerPublishedPackageCount", "coownedWithThirdPartyPackageCount",
            "thirdPartyOnlyPackageCount", "ownerlessPackageCount", "noVersionsPackageCount",
            "thirdPartyOnlyListedPackageCount", "thirdPartyOnlyUnlistedPackageCount",
            "thirdPartyOnlyUncertainPackageCount", "thirdPartyOnlyRecentStoredPublicationAndListedPackageCount"
        }.Concat(VersionCounts).Concat(DateFields).ToArray();

        // Created is row creation, not publication. PackagesCreatedDateDefaultValue gives both
        // Created and Published an insert-time default. PublishPackageAsync assigns Published
        // again; neither is an immutable first-publication event. Never substitute LastUpdated.
        // Include EVERY persisted version, including unlisted, deleted and validating rows.
        private static readonly string PackageProjection = @"
    SELECT pr.Id AS packageId,
        (SELECT COUNT_BIG(*) FROM dbo.PackageRegistrationOwners po
            WHERE po.PackageRegistrationKey = pr.[Key]) AS ownerCount,
        (SELECT COUNT_BIG(*) FROM dbo.PackageRegistrationOwners po
            WHERE po.PackageRegistrationKey = pr.[Key]
            AND po.UserKey IN (SELECT OwnerKey FROM RequestedOwners)) AS requestedOwnerCount,
        v.*
    FROM dbo.PackageRegistrations pr
    CROSS APPLY (
        SELECT COUNT_BIG(*) AS versionCount,
            COALESCE(SUM(CONVERT(bigint, CASE WHEN p.PackageStatusKey = 0 THEN 1 ELSE 0 END)), 0) AS availableVersionCount,
            COALESCE(SUM(CONVERT(bigint, CASE WHEN p.PackageStatusKey = 1 THEN 1 ELSE 0 END)), 0) AS deletedVersionCount,
            COALESCE(SUM(CONVERT(bigint, CASE WHEN p.PackageStatusKey = 2 THEN 1 ELSE 0 END)), 0) AS validatingVersionCount,
            COALESCE(SUM(CONVERT(bigint, CASE WHEN p.PackageStatusKey = 3 THEN 1 ELSE 0 END)), 0) AS failedValidationVersionCount,
            COALESCE(SUM(CONVERT(bigint, CASE WHEN p.PackageStatusKey NOT IN (0,1,2,3) OR p.PackageStatusKey IS NULL THEN 1 ELSE 0 END)), 0) AS unknownStatusVersionCount,
            COALESCE(SUM(CONVERT(bigint, CASE WHEN p.PackageStatusKey = 0 AND p.Listed = 1 THEN 1 ELSE 0 END)), 0) AS listedAvailableVersionCount,
            COALESCE(SUM(CONVERT(bigint, CASE WHEN p.Created >= @cutoff AND p.Created <= @assessment THEN 1 ELSE 0 END)), 0) AS createdSinceCutoffVersionCount,
            COALESCE(SUM(CONVERT(bigint, CASE WHEN p.PackageStatusKey = 0 AND p.Published >= @cutoff AND p.Published <= @assessment THEN 1 ELSE 0 END)), 0) AS publishedSinceCutoffAvailableVersionCount,
            COALESCE(SUM(CONVERT(bigint, CASE WHEN p.Created IS NULL OR p.Published IS NULL OR p.Created > @assessment
                OR p.Published > @assessment OR p.Published < p.Created THEN 1 ELSE 0 END)), 0) AS anomalousTimestampVersionCount,
            MIN(p.Created) AS earliestStoredCreatedUtc, MAX(p.Created) AS latestStoredCreatedUtc,
            MIN(p.Published) AS earliestStoredPublishedUtc, MAX(p.Published) AS latestStoredPublishedUtc
        FROM dbo.Packages p WHERE p.PackageRegistrationKey = pr.[Key]
    ) v
    WHERE (pr.Id COLLATE Latin1_General_100_CI_AS = @namespace OR pr.Id COLLATE Latin1_General_100_CI_AS LIKE @dottedPrefix ESCAPE '~')";

        // Aggregate one row per package ID, before taking the sample. Co-owned IDs never
        // enter third-party-only counts. A listed ID can also have uncertain statuses;
        // zero available versions (including no versions) must never imply unlisted.
        // Recent stored Published and current listing may come from different versions.
        // This is an observation of mutable stored timestamps, not verified activity.
        internal static readonly string Usage = OwnerScope + ", PackagesInScope AS (" + PackageProjection + @"
), Summary AS (
    SELECT COUNT_BIG(*) AS packageCount,
        (SELECT COUNT_BIG(*) FROM RequestedOwners) AS requestedOwnerCount,
        (SELECT COUNT_BIG(*) FROM RequestedOwners requestedOwner
            WHERE EXISTS (
                SELECT 1
                FROM dbo.PackageRegistrationOwners packageOwner
                INNER JOIN dbo.PackageRegistrations registration ON registration.[Key] = packageOwner.PackageRegistrationKey
                WHERE packageOwner.UserKey = requestedOwner.OwnerKey
                    AND (registration.Id COLLATE Latin1_General_100_CI_AS = @namespace
                        OR registration.Id COLLATE Latin1_General_100_CI_AS LIKE @dottedPrefix ESCAPE '~')
                    AND EXISTS (SELECT 1 FROM dbo.Packages package
                        WHERE package.PackageRegistrationKey = registration.[Key]
                            AND package.PackageStatusKey = 0))) AS requestedOwnerWithPublishedPackageCount,
        COALESCE(SUM(CONVERT(bigint, CASE WHEN requestedOwnerCount > 0 THEN 1 ELSE 0 END)), 0) AS requestedOwnerPackageCount,
        COALESCE(SUM(CONVERT(bigint, CASE WHEN requestedOwnerCount > 0 AND availableVersionCount > 0 THEN 1 ELSE 0 END)), 0) AS requestedOwnerPublishedPackageCount,
        COALESCE(SUM(CONVERT(bigint, CASE WHEN requestedOwnerCount > 0 AND ownerCount > requestedOwnerCount THEN 1 ELSE 0 END)), 0) AS coownedWithThirdPartyPackageCount,
        COALESCE(SUM(CONVERT(bigint, CASE WHEN requestedOwnerCount = 0 AND ownerCount > 0 THEN 1 ELSE 0 END)), 0) AS thirdPartyOnlyPackageCount,
        COALESCE(SUM(CONVERT(bigint, CASE WHEN ownerCount = 0 THEN 1 ELSE 0 END)), 0) AS ownerlessPackageCount,
        COALESCE(SUM(CONVERT(bigint, CASE WHEN versionCount = 0 THEN 1 ELSE 0 END)), 0) AS noVersionsPackageCount,
        COALESCE(SUM(CONVERT(bigint, CASE WHEN requestedOwnerCount = 0 AND ownerCount > 0
            AND listedAvailableVersionCount > 0 THEN 1 ELSE 0 END)), 0) AS thirdPartyOnlyListedPackageCount,
        COALESCE(SUM(CONVERT(bigint, CASE WHEN requestedOwnerCount = 0 AND ownerCount > 0
            AND availableVersionCount > 0 AND listedAvailableVersionCount = 0
            AND deletedVersionCount = 0 AND validatingVersionCount = 0
            AND failedValidationVersionCount = 0 AND unknownStatusVersionCount = 0 THEN 1 ELSE 0 END)), 0) AS thirdPartyOnlyUnlistedPackageCount,
        COALESCE(SUM(CONVERT(bigint, CASE WHEN requestedOwnerCount = 0 AND ownerCount > 0
            AND (availableVersionCount = 0 OR deletedVersionCount > 0 OR validatingVersionCount > 0
                OR failedValidationVersionCount > 0 OR unknownStatusVersionCount > 0) THEN 1 ELSE 0 END)), 0) AS thirdPartyOnlyUncertainPackageCount,
        COALESCE(SUM(CONVERT(bigint, CASE WHEN requestedOwnerCount = 0 AND ownerCount > 0
            AND publishedSinceCutoffAvailableVersionCount > 0 AND listedAvailableVersionCount > 0 THEN 1 ELSE 0 END)), 0) AS thirdPartyOnlyRecentStoredPublicationAndListedPackageCount,
        " + string.Join(",\n        ", VersionCounts.Select(f => "COALESCE(SUM(" + f + "), 0) AS " + f)) + @",
        MIN(earliestStoredCreatedUtc) AS earliestStoredCreatedUtc, MAX(latestStoredCreatedUtc) AS latestStoredCreatedUtc,
        MIN(earliestStoredPublishedUtc) AS earliestStoredPublishedUtc, MAX(latestStoredPublishedUtc) AS latestStoredPublishedUtc
    FROM PackagesInScope
)
SELECT " + string.Join(", ", UsageCounts.Select(f => "s." + f)) + ", "
            + string.Join(", ", PackageFields.Select(f => "d." + f + " AS sample_" + f)) + @"
FROM Summary s
OUTER APPLY (SELECT TOP (20) * FROM PackagesInScope ORDER BY packageId) d
ORDER BY d.packageId";

        internal static readonly string Details = OwnerScope + " SELECT TOP (10) "
            + string.Join(", ", PackageFields.Select(f => "d." + f))
            + " FROM (" + PackageProjection + " AND pr.Id COLLATE Latin1_General_100_CI_AS IN ("
            + string.Join(",", Enumerable.Range(0, 10).Select(i => "@package" + i))
            + ")) d ORDER BY d.packageId";
    }
}