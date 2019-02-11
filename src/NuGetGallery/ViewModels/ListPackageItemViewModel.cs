// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;
using System.Linq;
using System.Text;
using NuGet.Services.Entities;
using NuGetGallery.Helpers;

namespace NuGetGallery
{
    public class ListPackageItemViewModel : PackageViewModel
    {
        private const int _descriptionLengthLimit = 300;
        private const string _omissionString = "...";

        private string _signatureInformation;

        public ListPackageItemViewModel(Package package, User currentUser)
            : base(package)
        {
            Tags = package.Tags?
                .Split(' ')
                .Where(t => !string.IsNullOrEmpty(t))
                .Select(t => t.Trim())
                .ToArray();

            Authors = package.FlattenedAuthors;
            MinClientVersion = package.MinClientVersion;
            Owners = package.PackageRegistration?.Owners;
            IsVerified = package.PackageRegistration?.IsVerified;

            bool wasTruncated;
            ShortDescription = Description.TruncateAtWordBoundary(_descriptionLengthLimit, _omissionString, out wasTruncated);
            IsDescriptionTruncated = wasTruncated;

            DeprecationStatus = package.Deprecations.SingleOrDefault()?.Status ?? PackageDeprecationStatus.NotDeprecated;

            CanDisplayPrivateMetadata = CanPerformAction(currentUser, package, ActionsRequiringPermissions.DisplayPrivatePackageMetadata);
            CanEdit = CanPerformAction(currentUser, package, ActionsRequiringPermissions.EditPackage);
            CanUnlistOrRelist = CanPerformAction(currentUser, package, ActionsRequiringPermissions.UnlistOrRelistPackage);
            CanManageOwners = CanPerformAction(currentUser, package, ActionsRequiringPermissions.ManagePackageOwnership);
            CanReportAsOwner = CanPerformAction(currentUser, package, ActionsRequiringPermissions.ReportPackageAsOwner);
            CanSeeBreadcrumbWithProfile = CanPerformAction(currentUser, package, ActionsRequiringPermissions.ShowProfileBreadcrumb);
            CanDeleteSymbolsPackage = CanPerformAction(currentUser, package, ActionsRequiringPermissions.DeleteSymbolPackage);
            CanDeprecate = CanPerformAction(currentUser, package, ActionsRequiringPermissions.DeprecatePackage);
        }

        public string Authors { get; set; }
        public ICollection<User> Owners { get; set; }
        public IEnumerable<string> Tags { get; set; }
        public string MinClientVersion { get; set; }
        public string ShortDescription { get; set; }
        public bool IsDescriptionTruncated { get; set; }
        public bool? IsVerified { get; set; }
        public string SignatureInformation
        {
            get
            {
                if (CanDisplayPrivateMetadata && _signatureInformation == null)
                {
                    _signatureInformation = GetSignerInformation();
                }

                return _signatureInformation;
            }
        }

        public PackageDeprecationStatus DeprecationStatus { get; set; }

        public bool UseVersion
        {
            get
            {
                // only use the version in URLs when necessary. This would happen when the latest version is not the
                // same as the latest stable version.
                return !(!IsSemVer2 && LatestVersion && LatestStableVersion)
                    && !(IsSemVer2 && LatestStableVersionSemVer2 && LatestVersionSemVer2);
            }
        }

        public bool CanDisplayPrivateMetadata { get; set; }
        public bool CanEdit { get; set; }
        public bool CanUnlistOrRelist { get; set; }
        public bool CanManageOwners { get; set; }
        public bool CanReportAsOwner { get; set; }
        public bool CanSeeBreadcrumbWithProfile { get; set; }
        public bool CanDeleteSymbolsPackage { get; set; }
        public bool CanDeprecate { get; set; }

        private static bool CanPerformAction(User currentUser, Package package, ActionRequiringPackagePermissions action)
        {
            return action.CheckPermissionsOnBehalfOfAnyAccount(currentUser, package) == PermissionsCheckResult.Allowed;
        }

        private string GetSignerInformation()
        {
            if (_package.Certificate == null)
            {
                return null;
            }

            var owners = _package.PackageRegistration?.Owners ?? Enumerable.Empty<User>();
            var signers = owners.Where(owner => owner.UserCertificates.Any(uc => uc.CertificateKey == _package.CertificateKey));
            var signersCount = signers.Count();

            var builder = new StringBuilder();

            builder.Append($"Signed with");

            if (signersCount == 1)
            {
                builder.Append($" {signers.Single().Username}'s");
            }
            else if (signersCount == 2)
            {
                builder.Append($" {signers.First().Username} and {signers.Last().Username}'s");
            }
            else if (signersCount != 0)
            {
                foreach (var signer in signers.Take(signersCount - 1))
                {
                    builder.Append($" {signer.Username},");
                }

                builder.Append($" and {signers.Last().Username}'s");
            }

            builder.Append($" certificate ({_package.Certificate.Sha1Thumbprint})");

            return builder.ToString();
        }
    }
}