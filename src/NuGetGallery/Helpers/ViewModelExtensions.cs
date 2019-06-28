// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Web.Mvc;
using NuGet.Services.Entities;
using NuGet.Services.Licenses;
using NuGet.Versioning;
using NuGetGallery.Security;

namespace NuGetGallery
{
    public static class ViewModelExtensions
    {
        public static PackageViewModel SetupFromPackage(this PackageViewModel viewModel, Package package)
        {
            if (viewModel == null)
            {
                throw new ArgumentNullException(nameof(viewModel));
            }

            if (package == null)
            {
                throw new ArgumentNullException(nameof(package));
            }

            viewModel.FullVersion = NuGetVersionFormatter.ToFullString(package.Version);
            viewModel.IsSemVer2 = package.SemVerLevelKey == SemVerLevelKey.SemVer2;

            viewModel.Id = package.PackageRegistration.Id;
            viewModel.Version = String.IsNullOrEmpty(package.NormalizedVersion) ?
                NuGetVersionFormatter.Normalize(package.Version) :
                package.NormalizedVersion;

            viewModel.Description = package.Description;
            viewModel.ReleaseNotes = package.ReleaseNotes;
            viewModel.IconUrl = package.IconUrl;
            viewModel.LatestVersion = package.IsLatest;
            viewModel.LatestVersionSemVer2 = package.IsLatestSemVer2;
            viewModel.LatestStableVersion = package.IsLatestStable;
            viewModel.LatestStableVersionSemVer2 = package.IsLatestStableSemVer2;
            viewModel.DevelopmentDependency = package.DevelopmentDependency;
            viewModel.LastUpdated = package.Published;
            viewModel.Listed = package.Listed;
            viewModel.DownloadCount = package.DownloadCount;
            viewModel.Prerelease = package.IsPrerelease;
            viewModel.FailedValidation = package.PackageStatusKey == PackageStatus.FailedValidation;
            viewModel.Available = package.PackageStatusKey == PackageStatus.Available;
            viewModel.Validating = package.PackageStatusKey == PackageStatus.Validating;
            viewModel.Deleted = package.PackageStatusKey == PackageStatus.Deleted;
            viewModel.PackageStatusSummary = GetPackageStatusSummary(package.PackageStatusKey, package.Listed);
            viewModel.TotalDownloadCount = package.PackageRegistration.DownloadCount;
            viewModel.Title = string.IsNullOrEmpty(package.Title) ? package.PackageRegistration.Id : package.Title;

            return viewModel;
        }

        public static DisplayLicenseViewModel SetupFromPackage(
            this DisplayLicenseViewModel viewModel,
            Package package,
            IReadOnlyCollection<CompositeLicenseExpressionSegment> licenseExpressionSegments,
            string licenseFileContents)
        {
            ((PackageViewModel)viewModel).SetupFromPackage(package);

            viewModel.EmbeddedLicenseType = package.EmbeddedLicenseType;
            viewModel.LicenseExpression = package.LicenseExpression;
            if (PackageHelper.TryPrepareUrlForRendering(package.LicenseUrl, out string licenseUrl))
            {
                viewModel.LicenseUrl = licenseUrl;

                var licenseNames = package.LicenseNames;
                if (!string.IsNullOrEmpty(licenseNames))
                {
                    viewModel.LicenseNames = licenseNames.Split(',').Select(l => l.Trim());
                }
            }
            viewModel.LicenseExpressionSegments = licenseExpressionSegments;
            viewModel.LicenseFileContents = licenseFileContents;

            return viewModel;
        }

        public static ListPackageItemViewModel SetupFromPackage(this ListPackageItemViewModel viewModel, Package package, User currentUser)
        {
            ((PackageViewModel)viewModel).SetupFromPackage(package);

            viewModel.Tags = package.Tags?
                .Split(' ')
                .Where(t => !string.IsNullOrEmpty(t))
                .Select(t => t.Trim())
                .ToArray();

            viewModel.Authors = package.FlattenedAuthors;
            viewModel.MinClientVersion = package.MinClientVersion;
            viewModel.Owners = package.PackageRegistration?.Owners?.Select(GetBasicUserViewModel).ToList();
            viewModel.IsVerified = package.PackageRegistration?.IsVerified;

            viewModel.CanDisplayPrivateMetadata = CanPerformAction(currentUser, package, ActionsRequiringPermissions.DisplayPrivatePackageMetadata);
            viewModel.CanEdit = CanPerformAction(currentUser, package, ActionsRequiringPermissions.EditPackage);
            viewModel.CanUnlistOrRelist = CanPerformAction(currentUser, package, ActionsRequiringPermissions.UnlistOrRelistPackage);
            viewModel.CanManageOwners = CanPerformAction(currentUser, package, ActionsRequiringPermissions.ManagePackageOwnership);
            viewModel.CanReportAsOwner = CanPerformAction(currentUser, package, ActionsRequiringPermissions.ReportPackageAsOwner);
            viewModel.CanSeeBreadcrumbWithProfile = CanPerformAction(currentUser, package, ActionsRequiringPermissions.ShowProfileBreadcrumb);
            viewModel.CanDeleteSymbolsPackage = CanPerformAction(currentUser, package, ActionsRequiringPermissions.DeleteSymbolPackage);
            viewModel.CanDeprecate = CanPerformAction(currentUser, package, ActionsRequiringPermissions.DeprecatePackage);

            viewModel.SetShortDescriptionFrom(viewModel.Description);

            if (package.Certificate != null)
            {
                var owners = package.PackageRegistration?.Owners ?? Enumerable.Empty<User>();
                var signerUsernames = owners.Where(owner => owner.UserCertificates.Any(uc => uc.CertificateKey == package.CertificateKey)).Select(owner => owner.Username).ToList();
                viewModel.UpdateSignatureInformation(signerUsernames, package.Certificate.Sha1Thumbprint);
            }

            return viewModel;
        }

        // username must be an empty string because <select /> option values are based on username
        // and this "user" must be distinguishable from an account named "Any" and any other user;
        // null would be ideal, but null won't work as a <select /> option value.
        private static readonly SignerViewModel AnySigner =
            new SignerViewModel(username: "", displayText: "Any");

        public static ListPackageItemRequiredSignerViewModel SetupFromPackage(
            this ListPackageItemRequiredSignerViewModel viewModel,
            Package package,
            User currentUser,
            ISecurityPolicyService securityPolicyService,
            bool wasAADLoginOrMultiFactorAuthenticated)
        {
            ((ListPackageItemViewModel)viewModel).SetupFromPackage(package, currentUser);

            if (currentUser == null)
            {
                throw new ArgumentNullException(nameof(currentUser));
            }

            if (securityPolicyService == null)
            {
                throw new ArgumentNullException(nameof(securityPolicyService));
            }

            var owners = package.PackageRegistration?.Owners ?? Enumerable.Empty<User>();

            if (owners.Any())
            {
                viewModel.ShowRequiredSigner = true;

                viewModel.CanEditRequiredSigner = CanEditRequiredSigner(package, currentUser, securityPolicyService, owners);

                var requiredSigner = package.PackageRegistration?.RequiredSigners.FirstOrDefault();

                if (requiredSigner == null)
                {
                    if (owners.Count() == 1)
                    {
                        viewModel.RequiredSigner = GetSignerViewModel(owners.Single());
                    }
                    else
                    {
                        viewModel.RequiredSigner = AnySigner;
                    }
                }
                else
                {
                    viewModel.RequiredSigner = GetSignerViewModel(requiredSigner);
                }

                if (viewModel.CanEditRequiredSigner)
                {
                    if (owners.Count() == 1)
                    {
                        if (requiredSigner != null && requiredSigner != currentUser)
                        {
                            // Suppose users A and B own a package and user A is the required signer.
                            // Then suppose user A removes herself as package owner.
                            // User B must be able to change the required signer.
                            viewModel.AllSigners = new[] { viewModel.RequiredSigner, GetSignerViewModel(currentUser) };
                        }
                        else
                        {
                            viewModel.AllSigners = Enumerable.Empty<SignerViewModel>();
                            viewModel.CanEditRequiredSigner = false;
                            viewModel.ShowTextBox = true;
                        }
                    }
                    else
                    {
                        viewModel.AllSigners = new[] { AnySigner }.Concat(owners.Select(owner => GetSignerViewModel(owner)));
                    }
                }
                else
                {
                    viewModel.AllSigners = new[] { viewModel.RequiredSigner };

                    var ownersWithRequiredSignerControl = owners.Where(
                        owner => securityPolicyService.IsSubscribed(owner, ControlRequiredSignerPolicy.PolicyName));

                    if (owners.Count() == 1)
                    {
                        viewModel.ShowTextBox = true;
                    }
                    else
                    {
                        viewModel.UpdateRequiredSignerMessage(ownersWithRequiredSignerControl.Select(u => u.Username).ToList());
                    }
                }

                viewModel.CanEditRequiredSigner &= wasAADLoginOrMultiFactorAuthenticated;
            }

            return viewModel;
        }

        public static ManagePackageViewModel SetupFromPackage(
            this ManagePackageViewModel viewModel,
            Package package,
            User currentUser,
            IReadOnlyList<ReportPackageReason> reasons,
            UrlHelper url,
            string readMe,
            bool isManageDeprecationEnabled)
        {
            ((ListPackageItemViewModel)viewModel).SetupFromPackage(package, currentUser);

            viewModel.IsCurrentUserAnAdmin = currentUser != null && currentUser.IsAdministrator;

            viewModel.DeletePackagesRequest = new DeletePackagesRequest
            {
                Packages = new List<string>
                {
                    string.Format(
                        CultureInfo.InvariantCulture,
                        "{0}|{1}",
                        package.PackageRegistration.Id,
                        package.Version)
                },
                ReasonChoices = reasons
            };

            viewModel.IsLocked = package.PackageRegistration.IsLocked;

            viewModel.IsManageDeprecationEnabled = isManageDeprecationEnabled;

            var versionSelectPackages = package.PackageRegistration.Packages
                .Where(p => p.PackageStatusKey == PackageStatus.Available || p.PackageStatusKey == PackageStatus.Validating)
                .OrderByDescending(p => new NuGetVersion(p.Version))
                .ToList();

            viewModel.VersionSelectList = new List<SelectListItem>();
            viewModel.VersionListedStateDictionary = new Dictionary<string, ManagePackageViewModel.VersionListedState>();
            viewModel.VersionReadMeStateDictionary = new Dictionary<string, ManagePackageViewModel.VersionReadMeState>();
            viewModel.VersionDeprecationStateDictionary = new Dictionary<string, ManagePackageViewModel.VersionDeprecationState>();

            var submitUrlTemplate = url.PackageVersionActionTemplate("Edit");
            var getReadMeUrlTemplate = url.PackageVersionActionTemplate("GetReadMeMd");
            foreach (var versionSelectPackage in versionSelectPackages)
            {
                var text = PackageHelper.GetSelectListText(versionSelectPackage);
                var value = NuGetVersionFormatter.Normalize(versionSelectPackage.Version);
                viewModel.VersionSelectList.Add(new SelectListItem
                {
                    Text = text,
                    Value = value,
                    Selected = package == versionSelectPackage
                });

                viewModel.VersionListedStateDictionary.Add(
                    value,
                    new ManagePackageViewModel.VersionListedState(versionSelectPackage.Listed, versionSelectPackage.DownloadCount));

                var model = new TrivialPackageVersionModel(versionSelectPackage);
                viewModel.VersionReadMeStateDictionary.Add(
                    value,
                    new ManagePackageViewModel.VersionReadMeState(
                        submitUrlTemplate.Resolve(model),
                        getReadMeUrlTemplate.Resolve(model),
                        null));

                viewModel.VersionDeprecationStateDictionary.Add(
                    value,
                    GetVersionDeprecationState(versionSelectPackage.Deprecations.SingleOrDefault(), text));
            }

            // Update edit model with the readme.md data.
            viewModel.ReadMe = new EditPackageVersionReadMeRequest();
            if (package.HasReadMe)
            {
                viewModel.ReadMe.ReadMe.SourceType = ReadMeService.TypeWritten;
                viewModel.ReadMe.ReadMe.SourceText = readMe;
            }

            return viewModel;
        }

        public static DeleteAccountListPackageItemViewModel SetupFromPackage(
            this DeleteAccountListPackageItemViewModel viewModel,
            Package package,
            User userToDelete,
            User currentUser,
            IPackageService packageService)
        {
            ((ListPackageItemViewModel)viewModel).SetupFromPackage(package, currentUser);
            viewModel.WillBeOrphaned = packageService.WillPackageBeOrphanedIfOwnerRemoved(package.PackageRegistration, userToDelete);
            return viewModel;
        }

        public static DisplayPackageViewModel SetupFromPackage(
            this DisplayPackageViewModel viewModel,
            Package package,
            User currentUser,
            PackageDeprecation deprecation)
        {
            ((ListPackageItemViewModel)viewModel).SetupFromPackage(package, currentUser);

            var nuGetVersion = NuGetVersion.Parse(NuGetVersionFormatter.ToFullString(package.Version));

            viewModel.HasSemVer2Version = nuGetVersion.IsSemVer2;
            viewModel.HasSemVer2Dependency = package.Dependencies.ToList()
                .Where(pd => !string.IsNullOrEmpty(pd.VersionSpec))
                .Select(pd => VersionRange.Parse(pd.VersionSpec))
                .Any(p => (p.HasUpperBound && p.MaxVersion.IsSemVer2) || (p.HasLowerBound && p.MinVersion.IsSemVer2));

            viewModel.Dependencies = new DependencySetsViewModel(package.Dependencies);

            var packageHistory = package
                .PackageRegistration
                .Packages
                .OrderByDescending(p => new NuGetVersion(p.Version))
                .ToList();
            var pushedByCache = new Dictionary<User, string>();
            viewModel.PackageVersions = packageHistory.Select(p => GetPackageHistoryItemViewModel(p, currentUser, GetPushedBy(p, currentUser, pushedByCache))).ToList();

            viewModel.PushedBy = GetPushedBy(package, currentUser, pushedByCache);
            viewModel.PackageFileSize = package.PackageFileSize;

            viewModel.LatestSymbolsPackage = package.LatestSymbolPackage();
            viewModel.LatestAvailableSymbolsPackage = viewModel.LatestSymbolsPackage != null && viewModel.LatestSymbolsPackage.StatusKey == PackageStatus.Available
                ? viewModel.LatestSymbolsPackage
                : package.LatestAvailableSymbolPackage();

            if (packageHistory.Any())
            {
                // calculate the number of days since the package registration was created
                // round to the nearest integer, with a min value of 1
                // divide the total download count by this number
                viewModel.TotalDaysSinceCreated = Convert.ToInt32(Math.Max(1, Math.Round((DateTime.UtcNow - packageHistory.Min(p => p.Created)).TotalDays)));
                viewModel.DownloadsPerDay = viewModel.TotalDownloadCount / viewModel.TotalDaysSinceCreated; // for the package
                viewModel.DownloadsPerDayLabel = viewModel.DownloadsPerDay < 1 ? "<1" : viewModel.DownloadsPerDay.ToNuGetNumberString();
                viewModel.IsDotnetToolPackageType = package.PackageTypes.Any(e => e.Name.Equals("DotnetTool", StringComparison.OrdinalIgnoreCase));
            }

            if (deprecation != null)
            {
                viewModel.AlternatePackageId = deprecation.AlternatePackageRegistration?.Id;

                var alternatePackage = deprecation.AlternatePackage;
                if (alternatePackage != null)
                {
                    // A deprecation should not have both an alternate package registration and an alternate package.
                    // In case a deprecation does have both, we will hide the alternate package registration's ID in this model.
                    viewModel.AlternatePackageId = alternatePackage?.Id;
                    viewModel.AlternatePackageVersion = alternatePackage?.Version;
                }

                viewModel.CustomMessage = deprecation.CustomMessage;
            }

            return viewModel;
        }

        public static DeletePackageViewModel SetupFromPackage(
            this DeletePackageViewModel v,
            Package package,
            User currentUser,
            IReadOnlyList<ReportPackageReason> reasons)
        {
            ((DisplayPackageViewModel)v).SetupFromPackage(package, currentUser, deprecation: null);

            v.DeletePackagesRequest = new DeletePackagesRequest
            {
                Packages = new List<string>
                {
                    string.Format(
                        CultureInfo.InvariantCulture,
                        "{0}|{1}",
                        package.PackageRegistration.Id,
                        package.Version)
                },
                ReasonChoices = reasons
            };

            v.IsLocked = package.PackageRegistration.IsLocked;

            return v;
        }

        private static DisplayPackageViewModel GetPackageHistoryItemViewModel(
            Package package,
            User currentUser,
            string pushedBy)
        {
            var viewModel = new DisplayPackageViewModel();

            ((ListPackageItemViewModel)viewModel).SetupFromPackage(package, currentUser);

            viewModel.Copyright = package.Copyright;

            viewModel.DownloadCount = package.DownloadCount;
            viewModel.LastEdited = package.LastEdited;

            viewModel.TotalDaysSinceCreated = 0;
            viewModel.DownloadsPerDay = 0;

            viewModel.PushedBy = pushedBy;

            viewModel.InitializeRepositoryMetadata(package.RepositoryUrl, package.RepositoryType);

            if (PackageHelper.TryPrepareUrlForRendering(package.ProjectUrl, out string projectUrl))
            {
                viewModel.ProjectUrl = projectUrl;
            }

            viewModel.EmbeddedLicenseType = package.EmbeddedLicenseType;
            viewModel.LicenseExpression = package.LicenseExpression;

            if (PackageHelper.TryPrepareUrlForRendering(package.LicenseUrl, out string licenseUrl))
            {
                viewModel.LicenseUrl = licenseUrl;

                var licenseNames = package.LicenseNames;
                if (!string.IsNullOrEmpty(licenseNames))
                {
                    viewModel.LicenseNames = licenseNames.Split(',').Select(l => l.Trim());
                }
            }

            viewModel.DeprecationStatus = package.Deprecations.SingleOrDefault()?.Status
                ?? PackageDeprecationStatus.NotDeprecated;

            return viewModel;
        }

        private static PackageStatusSummary GetPackageStatusSummary(PackageStatus packageStatus, bool listed)
        {
            switch (packageStatus)
            {
                case PackageStatus.Validating:
                    {
                        return PackageStatusSummary.Validating;
                    }
                case PackageStatus.FailedValidation:
                    {
                        return PackageStatusSummary.FailedValidation;
                    }
                case PackageStatus.Available:
                    {
                        return listed ? PackageStatusSummary.Listed : PackageStatusSummary.Unlisted;
                    }
                case PackageStatus.Deleted:
                    {
                        return PackageStatusSummary.None;
                    }
                default:
                    throw new ArgumentOutOfRangeException(nameof(packageStatus));
            }
        }

        private static bool CanPerformAction(User currentUser, Package package, ActionRequiringPackagePermissions action)
        {
            return action.CheckPermissionsOnBehalfOfAnyAccount(currentUser, package) == PermissionsCheckResult.Allowed;
        }

        private static BasicUserViewModel GetBasicUserViewModel(User user)
        {
            return new BasicUserViewModel { Username = user.Username, EmailAddress = user.EmailAddress };
        }

        private static SignerViewModel GetSignerViewModel(User user)
        {
            if (user == null)
            {
                return null;
            }

            var certificatesCount = user.UserCertificates.Count();
            var displayText = $"{user.Username} ({certificatesCount} certificate{(certificatesCount == 1 ? string.Empty : "s")})";

            return new SignerViewModel(user.Username, displayText, certificatesCount > 0);
        }

        private static ManagePackageViewModel.VersionDeprecationState GetVersionDeprecationState(
            PackageDeprecation deprecation,
            string text)
        {
            var result = new ManagePackageViewModel.VersionDeprecationState();

            result.Text = text;

            if (deprecation != null)
            {
                result.IsLegacy = deprecation.Status.HasFlag(PackageDeprecationStatus.Legacy);
                result.HasCriticalBugs = deprecation.Status.HasFlag(PackageDeprecationStatus.CriticalBugs);
                result.IsOther = deprecation.Status.HasFlag(PackageDeprecationStatus.Other);

                result.AlternatePackageId = deprecation.AlternatePackageRegistration?.Id;

                var alternatePackage = deprecation.AlternatePackage;
                if (alternatePackage != null)
                {
                    // A deprecation should not have both an alternate package registration and an alternate package.
                    // In case a deprecation does have both, we will hide the alternate package registration's ID in this model.
                    result.AlternatePackageId = alternatePackage?.Id;
                    result.AlternatePackageVersion = alternatePackage?.Version;
                }

                result.CustomMessage = deprecation.CustomMessage;
            }

            return result;
        }

        private static bool CanEditRequiredSigner(Package package, User currentUser, ISecurityPolicyService securityPolicyService, IEnumerable<User> owners)
        {
            var currentUserCanManageRequiredSigner = false;
            var currentUserHasRequiredSignerControl = false;
            var noOwnerHasRequiredSignerControl = true;

            foreach (var owner in owners)
            {
                if (!currentUserCanManageRequiredSigner &&
                    ActionsRequiringPermissions.ManagePackageRequiredSigner.CheckPermissions(currentUser, owner, package)
                        == PermissionsCheckResult.Allowed)
                {
                    currentUserCanManageRequiredSigner = true;
                }

                if (!currentUserHasRequiredSignerControl)
                {
                    if (securityPolicyService.IsSubscribed(owner, ControlRequiredSignerPolicy.PolicyName))
                    {
                        noOwnerHasRequiredSignerControl = false;

                        if (owner == currentUser)
                        {
                            currentUserHasRequiredSignerControl = true;
                        }
                        else
                        {
                            currentUserHasRequiredSignerControl = (owner as Organization)?.GetMembershipOfUser(currentUser)?.IsAdmin ?? false;
                        }
                    }
                }
            }

            var canEditRequiredSigned = currentUserCanManageRequiredSigner &&
                (currentUserHasRequiredSignerControl || noOwnerHasRequiredSignerControl);
            return canEditRequiredSigned;
        }

        private static string GetPushedBy(Package package, User currentUser, Dictionary<User, string> pushedByCache)
        {
            var userPushedBy = package.User;

            if (userPushedBy == null)
            {
                return null;
            }

            if (!pushedByCache.ContainsKey(userPushedBy))
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
                        pushedByCache[userPushedBy] =
                            organizationsThatUserPushedByBelongsTo.Any(organization => ActionsRequiringPermissions.ViewAccount.CheckPermissions(currentUser, organization) == PermissionsCheckResult.Allowed) ?
                                userPushedBy?.Username :
                                organizationsThatUserPushedByBelongsTo.First().Username;
                    }
                    else
                    {
                        // Otherwise, show the user
                        pushedByCache[userPushedBy] = userPushedBy?.Username;
                    }
                }
                else
                {
                    pushedByCache[userPushedBy] = null;
                }
            }

            return pushedByCache[userPushedBy];
        }
    }
}