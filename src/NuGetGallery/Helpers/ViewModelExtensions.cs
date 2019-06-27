// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Web.Mvc;
using NuGet.Services.Entities;
using NuGet.Services.Licenses;
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
            viewModel.SetupFromPackage(package);

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
            viewModel.SetupFromPackage(package);
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
    }
}