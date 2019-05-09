// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using NuGet.Services.Entities;
using NuGetGallery.Security;

namespace NuGetGallery
{
    public sealed class ListPackageItemRequiredSignerViewModel : ListPackageItemViewModel
    {
        // username must be an empty string because <select /> option values are based on username
        // and this "user" must be distinguishable from an account named "Any" and any other user;
        // null would be ideal, but null won't work as a <select /> option value.
        private static readonly SignerViewModel AnySigner = 
            new SignerViewModel(username: "", displayText: "Any");

        public SignerViewModel RequiredSigner { get; set; }
        public string RequiredSignerMessage { get; set; }
        public IEnumerable<SignerViewModel> AllSigners { get; set; }
        public bool ShowRequiredSigner { get; set; }
        public bool ShowTextBox { get; set; }
        public bool CanEditRequiredSigner { get; set; }

        public ListPackageItemRequiredSignerViewModel(
            Package package,
            User currentUser,
            ISecurityPolicyService securityPolicyService,
            bool wasAADLoginOrMultiFactorAuthenticated,
            string overrideIconUrl)
            : base(package, currentUser, overrideIconUrl)
        {
            if (package == null)
            {
                throw new ArgumentNullException(nameof(package));
            }

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
                ShowRequiredSigner = true;

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

                CanEditRequiredSigner = currentUserCanManageRequiredSigner &&
                    (currentUserHasRequiredSignerControl || noOwnerHasRequiredSignerControl);

                var requiredSigner = package.PackageRegistration?.RequiredSigners.FirstOrDefault();

                if (requiredSigner == null)
                {
                    if (owners.Count() == 1)
                    {
                        RequiredSigner = Convert(owners.Single());
                    }
                    else
                    {
                        RequiredSigner = AnySigner;
                    }
                }
                else
                {
                    RequiredSigner = Convert(requiredSigner);
                }

                if (CanEditRequiredSigner)
                {
                    if (owners.Count() == 1)
                    {
                        if (requiredSigner != null && requiredSigner != currentUser)
                        {
                            // Suppose users A and B own a package and user A is the required signer.
                            // Then suppose user A removes herself as package owner.
                            // User B must be able to change the required signer.
                            AllSigners = new[] { RequiredSigner, Convert(currentUser) };
                        }
                        else
                        {
                            AllSigners = Enumerable.Empty<SignerViewModel>();
                            CanEditRequiredSigner = false;
                            ShowTextBox = true;
                        }
                    }
                    else
                    {
                        AllSigners = new[] { AnySigner }.Concat(owners.Select(owner => Convert(owner)));
                    }
                }
                else
                {
                    AllSigners = new[] { RequiredSigner };

                    var ownersWithRequiredSignerControl = owners.Where(
                        owner => securityPolicyService.IsSubscribed(owner, ControlRequiredSignerPolicy.PolicyName));

                    if (owners.Count() == 1)
                    {
                        ShowTextBox = true;
                    }
                    else
                    {
                        RequiredSignerMessage = GetRequiredSignerMessage(ownersWithRequiredSignerControl);
                    }
                }

                CanEditRequiredSigner &= wasAADLoginOrMultiFactorAuthenticated;
            }
        }

        private static SignerViewModel Convert(User user)
        {
            if (user == null)
            {
                return null;
            }

            var certificatesCount = user.UserCertificates.Count();
            var displayText = $"{user.Username} ({certificatesCount} certificate{(certificatesCount == 1 ? string.Empty : "s")})";

            return new SignerViewModel(user.Username, displayText, certificatesCount > 0);
        }

        private static string GetRequiredSignerMessage(IEnumerable<User> users)
        {
            var count = users.Count();

            if (count == 0)
            {
                return null;
            }

            var builder = new StringBuilder();

            builder.AppendFormat("The signing owner is managed by the ");

            if (count == 1)
            {
                builder.Append($"'{users.Single().Username}' account.");
            }
            else if (count == 2)
            {
                builder.Append($"'{users.First().Username}' and '{users.Last().Username}' accounts.");
            }
            else
            {
                foreach (var user in users.Take(count - 1))
                {
                    builder.Append($"'{user.Username}', ");
                }

                builder.Append($"and '{users.Last().Username}' accounts.");
            }

            return builder.ToString();
        }
    }
}