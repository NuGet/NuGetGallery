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
        public SignerViewModel RequiredSigner { get; set; }
        public string RequiredSignerMessage { get; private set; }
        public IEnumerable<SignerViewModel> AllSigners { get; set; }
        public bool ShowRequiredSigner { get; set; }
        public bool ShowTextBox { get; set; }
        public bool CanEditRequiredSigner { get; set; }

        public ListPackageItemRequiredSignerViewModel(
            Package package,
            User currentUser,
            ISecurityPolicyService securityPolicyService,
            bool wasAADLoginOrMultiFactorAuthenticated)
        {
            // TODO: remove
            this.Setup(package, currentUser, securityPolicyService, wasAADLoginOrMultiFactorAuthenticated);
        }

        public void UpdateRequiredSignerMessage(IReadOnlyCollection<string> signerUsernames)
        {
            RequiredSignerMessage = GetRequiredSignerMessage(signerUsernames);
        }

        private static string GetRequiredSignerMessage(IReadOnlyCollection<string> signerUsernames)
        {
            var count = signerUsernames.Count();

            if (count == 0)
            {
                return null;
            }

            var builder = new StringBuilder();

            builder.AppendFormat("The signing owner is managed by the ");

            if (count == 1)
            {
                builder.Append($"'{signerUsernames.Single()}' account.");
            }
            else if (count == 2)
            {
                builder.Append($"'{signerUsernames.First()}' and '{signerUsernames.Last()}' accounts.");
            }
            else
            {
                foreach (var username in signerUsernames.Take(count - 1))
                {
                    builder.Append($"'{username}', ");
                }

                builder.Append($"and '{signerUsernames.Last()}' accounts.");
            }

            return builder.ToString();
        }
    }
}