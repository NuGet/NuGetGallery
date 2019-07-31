// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using NuGet.Services.Entities;
using NuGetGallery.Security;

namespace NuGetGallery
{
    public static class ListPackageItemRequiredSignerViewModelExtensions
    {
        // username must be an empty string because <select /> option values are based on username
        // and this "user" must be distinguishable from an account named "Any" and any other user;
        // null would be ideal, but null won't work as a <select /> option value.
        private static readonly SignerViewModel AnySigner =
            new SignerViewModel(username: "", displayText: "Any");

        public static ListPackageItemRequiredSignerViewModel Setup(
            this ListPackageItemRequiredSignerViewModel viewModel,
            Package package,
            User currentUser,
            ISecurityPolicyService securityPolicyService,
            bool wasAADLoginOrMultiFactorAuthenticated,
            IIconUrlProvider iconUrlProvider)
        {
            ((ListPackageItemViewModel)viewModel).Setup(package, currentUser, iconUrlProvider);

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