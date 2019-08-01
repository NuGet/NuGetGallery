// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;
using System.Linq;
using NuGet.Services.Entities;
using NuGet.Services.Licenses;

namespace NuGetGallery
{
    public class DisplayLicenseViewModelHelper
    {
        private PackageViewModelHelper _packageViewModelHelper;

        public DisplayLicenseViewModelHelper()
        {
            _packageViewModelHelper = new PackageViewModelHelper();
        }

        public DisplayLicenseViewModel Create(
            Package package,
            IReadOnlyCollection<CompositeLicenseExpressionSegment> licenseExpressionSegments,
            string licenseFileContents)
        {
            var viewModel = new DisplayLicenseViewModel();
            return SetupDisplayLicenseViewModel(viewModel, package, licenseExpressionSegments, licenseFileContents);
        }

        private DisplayLicenseViewModel SetupDisplayLicenseViewModel(
            DisplayLicenseViewModel viewModel,
            Package package,
            IReadOnlyCollection<CompositeLicenseExpressionSegment> licenseExpressionSegments,
            string licenseFileContents)
        {
            _packageViewModelHelper.SetupPackageViewModel(viewModel, package);
            return SetupDisplayLicenseViewModelInternal(viewModel, package, licenseExpressionSegments, licenseFileContents);
        }

        private DisplayLicenseViewModel SetupDisplayLicenseViewModelInternal(
            DisplayLicenseViewModel viewModel,
            Package package,
            IReadOnlyCollection<CompositeLicenseExpressionSegment> licenseExpressionSegments,
            string licenseFileContents)
        {
            viewModel.EmbeddedLicenseType = package.EmbeddedLicenseType;
            viewModel.LicenseExpression = package.LicenseExpression;
            if (PackageHelper.TryPrepareUrlForRendering(package.LicenseUrl, out string licenseUrl))
            {
                viewModel.LicenseUrl = licenseUrl;

                var licenseNames = package.LicenseNames;
                if (!string.IsNullOrEmpty(licenseNames))
                {
                    viewModel.LicenseNames = licenseNames.Split(',').Select(l => l.Trim()).ToList();
                }
            }
            viewModel.LicenseExpressionSegments = licenseExpressionSegments;
            viewModel.LicenseFileContents = licenseFileContents;

            return viewModel;
        }
    }
}