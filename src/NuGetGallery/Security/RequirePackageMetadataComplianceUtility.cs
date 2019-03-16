// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Newtonsoft.Json;
using NuGet.Services.Entities;

namespace NuGetGallery.Security
{
    public static class RequirePackageMetadataComplianceUtility
    {
        /// <summary>
        /// Retrieve the policy state.
        /// </summary>
        public static RequirePackageMetadataState DeserializeState(IEnumerable<UserSecurityPolicy> policies)
        {
            var policyStates = policies
                .Where(p => !string.IsNullOrEmpty(p.Value))
                .Select(p => JsonConvert.DeserializeObject<RequirePackageMetadataState>(p.Value));

            // TODO: what if there are multiple?
            return policyStates.First();
        }

        public static bool IsPackageMetadataCompliant(Package package, RequirePackageMetadataState state, out IList<string> complianceFailures)
        {
            complianceFailures = new List<string>();

            // Author validation
            ValidatePackageAuthors(package, state, complianceFailures);

            // Copyright validation
            if (!state.AllowedCopyrightNotices.Contains(package.Copyright))
            {
                complianceFailures.Add(Strings.SecurityPolicy_CopyrightNotCompliant);
            }

            // LicenseUrl validation
            if (state.IsLicenseUrlRequired && string.IsNullOrWhiteSpace(package.LicenseUrl))
            {
                complianceFailures.Add(Strings.SecurityPolicy_RequiredLicenseUrlMissing);
            }

            // ProjectUrl validation
            if (state.IsProjectUrlRequired && string.IsNullOrWhiteSpace(package.ProjectUrl))
            {
                complianceFailures.Add(Strings.SecurityPolicy_RequiredProjectUrlMissing);
            }

            return !complianceFailures.Any();
        }

        private static void ValidatePackageAuthors(Package package, RequirePackageMetadataState state, IList<string> complianceFailures)
        {
            var packageAuthors = package.FlattenedAuthors
                .Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries)
                .Select(s => s.Trim())
                .ToList();

            // Check for duplicate entries
            var duplicateAuthors = packageAuthors
                .GroupBy(x => x)
                .Where(group => group.Count() > 1)
                .Select(group => group.Key)
                .ToList();

            if (duplicateAuthors.Any())
            {
                complianceFailures.Add(string.Format(CultureInfo.CurrentCulture, Strings.SecurityPolicy_PackageAuthorDuplicatesNotAllowed, string.Join(",", duplicateAuthors)));
            }
            else
            {
                if (state.AllowedAuthors?.Length > 0)
                {
                    foreach (var packageAuthor in packageAuthors)
                    {
                        if (!state.AllowedAuthors.Contains(packageAuthor))
                        {
                            complianceFailures.Add(string.Format(CultureInfo.CurrentCulture, Strings.SecurityPolicy_PackageAuthorNotAllowed, packageAuthor));
                        }
                    }
                }
                else
                {
                    // No list of allowed authors is defined for this policy.
                    // We require the required co-owner to be defined as the only package author.
                    if (packageAuthors.Count() > 1 || packageAuthors.Single() != state.RequiredCoOwnerUsername)
                    {
                        complianceFailures.Add(string.Format(CultureInfo.CurrentCulture, Strings.SecurityPolicy_RequiredAuthorMissing, state.RequiredCoOwnerUsername));
                    }
                }
            }
        }
    }
}