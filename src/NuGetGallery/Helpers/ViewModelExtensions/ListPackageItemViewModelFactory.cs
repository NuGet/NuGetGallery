// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Linq;
using NuGet.Services.Entities;

namespace NuGetGallery
{
    public class ListPackageItemViewModelFactory
    {
        private readonly PackageViewModelFactory _packageViewModelFactory;

        public ListPackageItemViewModelFactory(IIconUrlProvider iconUrlProvider)
        {
            _packageViewModelFactory = new PackageViewModelFactory(iconUrlProvider);
        }

        public ListPackageItemViewModel Create(Package package, User currentUser)
        {
            var viewModel = new ListPackageItemViewModel();
            return Setup(viewModel, package, currentUser);
        }

        public ListPackageItemViewModel Setup(ListPackageItemViewModel viewModel, Package package, User currentUser)
        {
            _packageViewModelFactory.Setup(viewModel, package);
            return SetupInternal(viewModel, package, currentUser);
        }

        private ListPackageItemViewModel SetupInternal(ListPackageItemViewModel viewModel, Package package, User currentUser)
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

        private static bool CanPerformAction(User currentUser, Package package, ActionRequiringPackagePermissions action)
        {
            return action.CheckPermissionsOnBehalfOfAnyAccount(currentUser, package) == PermissionsCheckResult.Allowed;
        }

        private static BasicUserViewModel GetBasicUserViewModel(User user)
        {
            return new BasicUserViewModel { Username = user.Username, EmailAddress = user.EmailAddress, IsOrganization = user is Organization };
        }
    }
}