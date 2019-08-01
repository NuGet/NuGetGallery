// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using NuGet.Services.Entities;
using NuGetGallery.Security;

namespace NuGetGallery
{
    public class ListPackageItemRequiredSignerViewModelHelper
    {
        private readonly ListPackageItemViewModelHelper _listPackageItemViewModel;
        private readonly ISecurityPolicyService _securityPolicyService;

        public ListPackageItemRequiredSignerViewModelHelper(ISecurityPolicyService securityPolicyService)
        {
            _listPackageItemViewModel = new ListPackageItemViewModelHelper();
            _securityPolicyService = securityPolicyService ?? throw new ArgumentNullException(nameof(securityPolicyService));
        }

        // username must be an empty string because <select /> option values are based on username
        // and this "user" must be distinguishable from an account named "Any" and any other user;
        // null would be ideal, but null won't work as a <select /> option value.
        private static readonly SignerViewModel AnySigner =
            new SignerViewModel(username: "", displayText: "Any");

        public ListPackageItemRequiredSignerViewModel Create(
            Package package,
            User currentUser,
            bool wasAADLoginOrMultiFactorAuthenticated)
        {
            var viewModel = new ListPackageItemRequiredSignerViewModel();
            return SetupListPackageItemRequiredSignerViewModel(viewModel, package, currentUser, wasAADLoginOrMultiFactorAuthenticated);
        }

        public ListPackageItemRequiredSignerViewModel SetupListPackageItemRequiredSignerViewModel(
            ListPackageItemRequiredSignerViewModel viewModel,
            Package package,
            User currentUser,
            bool wasAADLoginOrMultiFactorAuthenticated)
        {
            _listPackageItemViewModel.SetupListPackageItemViewModel(viewModel, package, currentUser);
            return SetupListPackageItemRequiredSignerViewModelInternal(viewModel, package, currentUser, wasAADLoginOrMultiFactorAuthenticated);
        }

        private ListPackageItemRequiredSignerViewModel SetupListPackageItemRequiredSignerViewModelInternal(
            ListPackageItemRequiredSignerViewModel viewModel,
            Package package,
            User currentUser,
            bool wasAADLoginOrMultiFactorAuthenticated)
        {
            if (currentUser == null)
            {
                throw new ArgumentNullException(nameof(currentUser));
            }

            var owners = package.PackageRegistration?.Owners ?? Enumerable.Empty<User>();

            if (owners.Any())
            {
                viewModel.ShowRequiredSigner = true;

                viewModel.CanEditRequiredSigner = CanEditRequiredSigner(package, currentUser, _securityPolicyService, owners);

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
                            viewModel.AllSigners = new List<SignerViewModel>();
                            viewModel.CanEditRequiredSigner = false;
                            viewModel.ShowTextBox = true;
                        }
                    }
                    else
                    {
                        viewModel.AllSigners = new[] { AnySigner }.Concat(owners.Select(owner => GetSignerViewModel(owner))).ToList();
                    }
                }
                else
                {
                    viewModel.AllSigners = new[] { viewModel.RequiredSigner };

                    var ownersWithRequiredSignerControl = owners.Where(
                        owner => _securityPolicyService.IsSubscribed(owner, ControlRequiredSignerPolicy.PolicyName));

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
    }
}