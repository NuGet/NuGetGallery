// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
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
        private IReadOnlyCollection<BasicUserViewModel> _signers;
        private string _sha1Thumbprint;

        public ListPackageItemViewModel(Package package, User currentUser)
        {
            // TODO: remove
            this.SetupFromPackage(package, currentUser);
        }

        public string Authors { get; set; }
        public ICollection<BasicUserViewModel> Owners { get; set; }
        public ICollection<string> Tags { get; set; }
        public string MinClientVersion { get; set; }
        public string ShortDescription { get; private set; }
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

        public void SetShortDescriptionFrom(string fullDescription)
        {
            ShortDescription = fullDescription.TruncateAtWordBoundary(_descriptionLengthLimit, _omissionString, out var wasTruncated);
            IsDescriptionTruncated = wasTruncated;
        }

        public void UpdateSignatureInformation(IReadOnlyCollection<BasicUserViewModel> signers, string sha1Thumbprint)
        {
            if ((signers == null && sha1Thumbprint != null) || (signers != null && sha1Thumbprint == null))
            {
                throw new ArgumentException($"{nameof(signers)} and {nameof(sha1Thumbprint)} arguments must either be both null or both non-null.");
            }

            _signers = signers;
            _sha1Thumbprint = sha1Thumbprint;
            _signatureInformation = null;
        }

        private string GetSignerInformation()
        {
            if (_signers == null)
            {
                return null;
            }

            var signersCount = _signers.Count();

            var builder = new StringBuilder();

            builder.Append($"Signed with");

            if (signersCount == 1)
            {
                builder.Append($" {_signers.Single().Username}'s");
            }
            else if (signersCount == 2)
            {
                builder.Append($" {_signers.First().Username} and {_signers.Last().Username}'s");
            }
            else if (signersCount != 0)
            {
                foreach (var signer in _signers.Take(signersCount - 1))
                {
                    builder.Append($" {signer.Username},");
                }

                builder.Append($" and {_signers.Last().Username}'s");
            }

            builder.Append($" certificate ({_sha1Thumbprint})");

            return builder.ToString();
        }
    }
}