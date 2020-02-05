// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using NuGet.Services.Entities;

namespace NuGetGallery
{
    public class PackageViewModelFactory
    {
        private readonly IIconUrlProvider _iconUrlProvider;

        public PackageViewModelFactory(IIconUrlProvider iconUrlProvider)
        {
            _iconUrlProvider = iconUrlProvider ?? throw new ArgumentNullException(nameof(iconUrlProvider));
        }

        public PackageViewModel Create(Package package)
        {
            var viewModel = new PackageViewModel();
            return Setup(viewModel, package);
        }

        public PackageViewModel Setup(PackageViewModel viewModel, Package package)
        {
            if (viewModel == null)
            {
                throw new ArgumentNullException(nameof(viewModel));
            }

            if (package == null)
            {
                throw new ArgumentNullException(nameof(package));
            }

            viewModel.FullVersion = NuGetVersionFormatter.ToFullString(package.Version);

            viewModel.Id = package.PackageRegistration.Id;
            viewModel.Version = String.IsNullOrEmpty(package.NormalizedVersion) ?
                NuGetVersionFormatter.Normalize(package.Version) :
                package.NormalizedVersion;

            viewModel.Description = package.Description;
            viewModel.ReleaseNotes = package.ReleaseNotes;
            viewModel.IconUrl = _iconUrlProvider.GetIconUrlString(package);
            viewModel.LatestVersionSemVer2 = package.IsLatestSemVer2;
            viewModel.LatestStableVersionSemVer2 = package.IsLatestStableSemVer2;
            viewModel.DevelopmentDependency = package.DevelopmentDependency;
            viewModel.LastUpdated = package.Published;
            viewModel.Listed = package.Listed;
            viewModel.DownloadCount = package.DownloadCount;
            viewModel.Prerelease = package.IsPrerelease;
            viewModel.FailedValidation = package.PackageStatusKey == PackageStatus.FailedValidation;
            viewModel.Available = package.PackageStatusKey == PackageStatus.Available;
            viewModel.Validating = package.PackageStatusKey == PackageStatus.Validating;
            viewModel.Deleted = package.PackageStatusKey == PackageStatus.Deleted;
            viewModel.PackageStatusSummary = GetPackageStatusSummary(package.PackageStatusKey, package.Listed);
            viewModel.TotalDownloadCount = package.PackageRegistration.DownloadCount;

            return viewModel;
        }

        private static PackageStatusSummary GetPackageStatusSummary(PackageStatus packageStatus, bool listed)
        {
            switch (packageStatus)
            {
                case PackageStatus.Validating:
                    {
                        return PackageStatusSummary.Validating;
                    }
                case PackageStatus.FailedValidation:
                    {
                        return PackageStatusSummary.FailedValidation;
                    }
                case PackageStatus.Available:
                    {
                        return listed ? PackageStatusSummary.Listed : PackageStatusSummary.Unlisted;
                    }
                case PackageStatus.Deleted:
                    {
                        return PackageStatusSummary.None;
                    }
                default:
                    throw new ArgumentOutOfRangeException(nameof(packageStatus));
            }
        }
    }
}