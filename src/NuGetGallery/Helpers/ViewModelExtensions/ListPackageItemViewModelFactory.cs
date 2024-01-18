// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Linq;
using NuGet.Services.Entities;
using NuGetGallery;
using NuGetGallery.Frameworks;
using NuGetGallery.Helpers;

namespace NuGetGallery
{
    public class ListPackageItemViewModelFactory
    {
        private readonly PackageViewModelFactory _packageViewModelFactory;
        private readonly IPackageFrameworkCompatibilityFactory _frameworkCompatibilityFactory;
        private readonly IFeatureFlagService _featureFlagService;

        public ListPackageItemViewModelFactory(IIconUrlProvider iconUrlProvider, IPackageFrameworkCompatibilityFactory frameworkCompatibilityFactory, IFeatureFlagService featureFlagService)
        {
            _packageViewModelFactory = new PackageViewModelFactory(iconUrlProvider);
            _frameworkCompatibilityFactory = frameworkCompatibilityFactory;
            _featureFlagService = featureFlagService ?? throw new ArgumentNullException(nameof(featureFlagService));
        }

        public ListPackageItemViewModel Create(Package package, User currentUser, bool includeComputedBadges = false)
        {
            var viewModel = new ListPackageItemViewModel();
            return Setup(viewModel, package, currentUser, includeComputedBadges);
        }

        public ListPackageItemViewModel Setup(ListPackageItemViewModel viewModel, Package package, User currentUser, bool includeComputedBadges = false)
        {
            _packageViewModelFactory.Setup(viewModel, package);
            return SetupInternal(viewModel, package, currentUser, includeComputedBadges);
        }

        private ListPackageItemViewModel SetupInternal(ListPackageItemViewModel viewModel, Package package, User currentUser, bool includeComputedBadges = false)
        {
            viewModel.Tags = package.Tags?
                .Split(' ')
                .Where(t => !string.IsNullOrEmpty(t))
                .Select(t => t.Trim())
                .ToArray();

            viewModel.Authors = package.FlattenedAuthors;
            viewModel.MinClientVersion = package.MinClientVersion;
            viewModel.Owners = package.PackageRegistration?.Owners?.Select(GetBasicUserViewModel).ToList();
            viewModel.IsVerified = package.PackageRegistration?.IsVerified;
            viewModel.IsDeprecated = package.Deprecations?.Count > 0;
            viewModel.IsVulnerable = package.VulnerablePackageRanges?.Count > 0;

            if (viewModel.IsDeprecated)
            {
                viewModel.DeprecationTitle = WarningTitleHelper.GetDeprecationTitle(package.Version, package.Deprecations.First().Status);
            }

            if (viewModel.IsVulnerable)
            {
                var maxVulnerabilitySeverity = package.VulnerablePackageRanges.Max(vpr => vpr.Vulnerability.Severity);
                viewModel.VulnerabilityTitle = WarningTitleHelper.GetVulnerabilityTitle(package.Version, maxVulnerabilitySeverity);
            }

            viewModel.CanDisplayPrivateMetadata = CanPerformAction(currentUser, package, ActionsRequiringPermissions.DisplayPrivatePackageMetadata);
            viewModel.CanEdit = CanPerformAction(currentUser, package, ActionsRequiringPermissions.EditPackage);
            viewModel.CanUnlistOrRelist = CanPerformAction(currentUser, package, ActionsRequiringPermissions.UnlistOrRelistPackage);
            viewModel.CanManageOwners = CanPerformAction(currentUser, package, ActionsRequiringPermissions.ManagePackageOwnership);
            viewModel.CanReportAsOwner = CanPerformAction(currentUser, package, ActionsRequiringPermissions.ReportPackageAsOwner);
            viewModel.CanSeeBreadcrumbWithProfile = CanPerformAction(currentUser, package, ActionsRequiringPermissions.ShowProfileBreadcrumb);
            viewModel.CanDeleteSymbolsPackage = CanPerformAction(currentUser, package, ActionsRequiringPermissions.DeleteSymbolPackage);
            viewModel.CanDeprecate = CanPerformAction(currentUser, package, ActionsRequiringPermissions.DeprecatePackage);
            viewModel.CanDisplayTfmBadges = _featureFlagService.IsDisplayTfmBadgesEnabled(currentUser);

            PackageFrameworkCompatibility packageFrameworkCompatibility = _frameworkCompatibilityFactory.Create(package.SupportedFrameworks, package.Id, package.Version.ToString(), includeComputedBadges);
            viewModel.FrameworkBadges = viewModel.CanDisplayTfmBadges ? packageFrameworkCompatibility?.Badges : new PackageFrameworkCompatibilityBadges();

            viewModel.SetShortDescriptionFrom(viewModel.Description);

            if (package.Certificate != null)
            {
                var owners = package.PackageRegistration?.Owners ?? Enumerable.Empty<User>();
                var signerUsernames = owners.Where(owner => owner.UserCertificates.Any(uc => uc.CertificateKey == package.CertificateKey)).Select(owner => owner.Username).ToList();
                viewModel.UpdateSignatureInformation(signerUsernames, package.Certificate.Thumbprint);
            }

            return viewModel;
        }

        private static bool CanPerformAction(User currentUser, Package package, ActionRequiringPackagePermissions action)
        {
            return action.CheckPermissionsOnBehalfOfAnyAccount(currentUser, package) == PermissionsCheckResult.Allowed;
        }

        private static BasicUserViewModel GetBasicUserViewModel(User user)
        {
            return new BasicUserViewModel
            {
                Username = user.Username,
                EmailAddress = user.EmailAddress,
                IsOrganization = user is Organization,
                IsLocked = user.IsLocked
            };
        }
    }
}