// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using NuGetGallery.Helpers;

namespace NuGetGallery
{
    public class ListPackageItemViewModel : PackageViewModel
    {
        private const int _descriptionLengthLimit = 300;
        private const string _omissionString = "...";

        private string _signatureInformation;
        private IReadOnlyCollection<string> _signerUsernames;
        private string _thumbprint;

        public string Authors { get; set; }
        public IReadOnlyCollection<BasicUserViewModel> Owners { get; set; }
        public IReadOnlyCollection<string> Tags { get; set; }
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
                return !(LatestStableVersionSemVer2 && LatestVersionSemVer2);
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

        public void UpdateSignatureInformation(IReadOnlyCollection<string> signerUsernames, string thumbprint)
        {
            if ((signerUsernames == null && thumbprint != null) || (signerUsernames != null && thumbprint == null))
            {
                throw new ArgumentException($"{nameof(signerUsernames)} and {nameof(thumbprint)} arguments must either be both null or both non-null.");
            }

            _signerUsernames = signerUsernames;
            _thumbprint = thumbprint;
            _signatureInformation = null;
        }

        private string GetSignerInformation()
        {
            if (_signerUsernames == null)
            {
                return null;
            }

            var signersCount = _signerUsernames.Count;

            var builder = new StringBuilder();

            builder.Append($"Signed with");

            if (signersCount == 1)
            {
                builder.Append($" {_signerUsernames.Single()}'s");
            }
            else if (signersCount == 2)
            {
                builder.Append($" {_signerUsernames.First()} and {_signerUsernames.Last()}'s");
            }
            else if (signersCount != 0)
            {
                foreach (var signerUsername in _signerUsernames.Take(signersCount - 1))
                {
                    builder.Append($" {signerUsername},");
                }

                builder.Append($" and {_signerUsernames.Last()}'s");
            }

            builder.Append($" certificate ({_thumbprint})");

            return builder.ToString();
        }
    }
}