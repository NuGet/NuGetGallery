// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.
using NuGetGallery.Helpers;
using System.Collections.Generic;
using System.Linq;
using System.Security.Principal;

namespace NuGetGallery
{
    public class ListPackageItemViewModel : PackageViewModel
    {
        private const int _descriptionLengthLimit = 300;
        private const string _omissionString = "...";

        public ListPackageItemViewModel(Package package)
            : base(package)
        {
            Tags = package.Tags?.Trim().Split(' ');

            Authors = package.FlattenedAuthors;
            MinClientVersion = package.MinClientVersion;
            Owners = package.PackageRegistration?.Owners;

            bool wasTruncated;
            ShortDescription = Description.TruncateAtWordBoundary(_descriptionLengthLimit, _omissionString, out wasTruncated);
            IsDescriptionTruncated = wasTruncated;
        }

        public string Authors { get; set; }
        public ICollection<User> Owners { get; set; }
        public IEnumerable<string> Tags { get; set; }
        public string MinClientVersion { get; set; }
        public string ShortDescription { get; set; }
        public bool IsDescriptionTruncated { get; set; }
        public string PolicyMessage { get; set; }

        public bool UseVersion
        {
            get
            {
                // only use the version in URLs when necessary. This would happen when the latest version is not the same as the latest stable version.
                return !(!IsSemVer2 && LatestVersion && LatestStableVersion) 
                    && !(IsSemVer2 && LatestStableVersionSemVer2 && LatestVersionSemVer2);
            }
        }

        public bool IsOwner(IPrincipal user)
        {
            if (user == null || user.Identity == null)
            {
                return false;
            }
            return Owners.Any(u => u.Username == user.Identity.Name);
        }
    }
}