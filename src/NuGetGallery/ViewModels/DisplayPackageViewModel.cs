// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using NuGet.Services.Validation.Issues;
using NuGet.Versioning;

namespace NuGetGallery
{
    public class DisplayPackageViewModel : ListPackageItemViewModel
    {
        public DisplayPackageViewModel(Package package, User currentUser, IOrderedEnumerable<Package> packageHistory)
            : this(package, currentUser, (string)null)
        {
            HasSemVer2Version = NuGetVersion.IsSemVer2;
            HasSemVer2Dependency = package.Dependencies.ToList()
                .Where(pd => !string.IsNullOrEmpty(pd.VersionSpec))
                .Select(pd => VersionRange.Parse(pd.VersionSpec))
                .Any(p => (p.HasUpperBound && p.MaxVersion.IsSemVer2) || (p.HasLowerBound && p.MinVersion.IsSemVer2));

            Dependencies = new DependencySetsViewModel(package.Dependencies);
            PackageVersions = packageHistory.Select(p => new DisplayPackageViewModel(p, currentUser, GetPushedBy(p, currentUser)));

            PushedBy = GetPushedBy(package, currentUser);

            if (packageHistory.Any())
            {
                // calculate the number of days since the package registration was created
                // round to the nearest integer, with a min value of 1
                // divide the total download count by this number
                TotalDaysSinceCreated = Convert.ToInt32(Math.Max(1, Math.Round((DateTime.UtcNow - packageHistory.Min(p => p.Created)).TotalDays)));
                DownloadsPerDay = TotalDownloadCount / TotalDaysSinceCreated; // for the package
                DownloadsPerDayLabel = DownloadsPerDay < 1 ? "<1" : DownloadsPerDay.ToNuGetNumberString();
            }
        }

        public DisplayPackageViewModel(Package package, User currentUser, string pushedBy)
            : base(package, currentUser)
        {
            Copyright = package.Copyright;
            
            DownloadCount = package.DownloadCount;
            LastEdited = package.LastEdited;
            
            TotalDaysSinceCreated = 0;
            DownloadsPerDay = 0;

            PushedBy = pushedBy;
        }

        public bool ValidatingTooLong { get; set; }
        public IReadOnlyList<ValidationIssue> ValidationIssues { get; set; }
        public DependencySetsViewModel Dependencies { get; set; }
        public IEnumerable<DisplayPackageViewModel> PackageVersions { get; set; }
        public string Copyright { get; set; }
        public string ReadMeHtml { get; set; }
        public DateTime? LastEdited { get; set; }
        public int DownloadsPerDay { get; private set; }
        public int TotalDaysSinceCreated { get; private set; }

        public bool HasSemVer2Version { get; }
        public bool HasSemVer2Dependency { get; }

        public bool HasNewerPrerelease
        {
            get
            {
                var latestPrereleaseVersion = PackageVersions
                    .Where(pv => pv.Prerelease && pv.Available && pv.Listed)
                    .Max(pv => pv.NuGetVersion);

                return latestPrereleaseVersion > NuGetVersion;
            }
        }

        public bool? IsIndexed { get; set; }

        public string DownloadsPerDayLabel { get; private set; }

        public string PushedBy { get; private set; }

        private IDictionary<User, string> _pushedByCache = new Dictionary<User, string>();

        private string GetPushedBy(Package package, User currentUser)
        {
            var userPushedBy = package.User;

            if (userPushedBy == null)
            {
                return null;
            }

            if (!_pushedByCache.ContainsKey(userPushedBy))
            {
                // Only show who pushed the package version to users that can see private package metadata
                if (ActionsRequiringPermissions.DisplayPrivatePackageMetadata.CheckPermissionsOnBehalfOfAnyAccount(currentUser, package) == PermissionsCheckResult.Allowed)
                {
                    var organizationsThatUserPushedByBelongsTo =
                        (package.PackageRegistration?.Owners ?? Enumerable.Empty<User>())
                            .OfType<Organization>()
                            .Where(organization => ActionsRequiringPermissions.ViewAccount.CheckPermissions(userPushedBy, organization) == PermissionsCheckResult.Allowed);
                    if (organizationsThatUserPushedByBelongsTo.Any())
                    {
                        // If the user is a member of any organizations that are package owners, only show the user if the current user is a member of the same organization
                        _pushedByCache[userPushedBy] =
                            organizationsThatUserPushedByBelongsTo.Any(organization => ActionsRequiringPermissions.ViewAccount.CheckPermissions(currentUser, organization) == PermissionsCheckResult.Allowed) ?
                                userPushedBy?.Username :
                                organizationsThatUserPushedByBelongsTo.First().Username;
                    }
                    else
                    {
                        // Otherwise, show the user
                        _pushedByCache[userPushedBy] = userPushedBy?.Username;
                    }
                }
                else
                {
                    _pushedByCache[userPushedBy] = null;
                }
            }

            return _pushedByCache[userPushedBy];
        }
    }
}