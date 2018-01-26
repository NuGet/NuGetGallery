// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;
using System.Linq;
using System.Security.Principal;
using NuGetGallery.Helpers;

namespace NuGetGallery
{
    public class ListPackageItemViewModel : PackageViewModel
    {
        private const int _descriptionLengthLimit = 300;
        private const string _omissionString = "...";

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

            CanDisplayPrivateMetadata = CanPerformAction(currentUser, package, ActionsRequiringPermissions.DisplayPrivatePackageMetadata);
            CanEdit = CanPerformAction(currentUser, package, ActionsRequiringPermissions.EditPackage);
            CanUnlistOrRelist = CanPerformAction(currentUser, package, ActionsRequiringPermissions.UnlistOrRelistPackage);
            CanManageOwners = CanPerformAction(currentUser, package, ActionsRequiringPermissions.ManagePackageOwnership);
            CanReportAsOwner = CanPerformAction(currentUser, package, ActionsRequiringPermissions.ReportPackageAsOwner);
        }

        public string Authors { get; set; }
        public ICollection<User> Owners { get; set; }
        public IEnumerable<string> Tags { get; set; }
        public string MinClientVersion { get; set; }
        public string ShortDescription { get; set; }
        public bool IsDescriptionTruncated { get; set; }
        public bool? IsVerified { get; set; }

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

        public bool HasSingleOwner
        {
            get
            {
                var userAccountOwners = Owners.Where(o => !(o is Organization)).Distinct().ToList();
                if (userAccountOwners.Count() > 1)
                {
                    return false;
                }

                var organizationAccountOwners = Owners.Where(o => o is Organization).ToList();
                foreach(var o in organizationAccountOwners)
                {
                    userAccountOwners = userAccountOwners.Union(OrganizationExtensions.GetUserAccountMembers((Organization)o)).ToList();
                    if(userAccountOwners.Count() > 1)
                    {
                        return false;
                    }
                }

                return userAccountOwners.Any();
            }
        }

        public bool CanDisplayPrivateMetadata { get; set; }
        public bool CanEdit { get; set; }
        public bool CanUnlistOrRelist { get; set; }
        public bool CanManageOwners { get; set; }
        public bool CanReportAsOwner { get; set; }

        private static bool CanPerformAction(User currentUser, Package package, ActionRequiringPackagePermissions action)
        {
            return action.CheckPermissionsOnBehalfOfAnyAccount(currentUser, package) == PermissionsCheckResult.Allowed;
        }
    }
}