// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using Xunit;

namespace NuGetGallery.ViewModels
{
    public class PackageViewModelFacts
    {
        [Fact]
        public void UsesNormalizedVersionForDisplay()
        {
            var package = new Package()
            {
                Version = "01.02.00.00",
                NormalizedVersion = "1.3.0" // Different just to prove the View Model is using the DB column.
            };
            var packageViewModel = new PackageViewModel(package);
            Assert.Equal("1.3.0", packageViewModel.Version);
        }

        [Fact]
        public void UsesNormalizedPackageVersionIfNormalizedVersionMissing()
        {
            var package = new Package()
            {
                Version = "01.02.00.00"
            };
            var packageViewModel = new PackageViewModel(package);
            Assert.Equal("1.2.0", packageViewModel.Version);
        }

        [Fact]
        public void LicenseNamesAreParsedByCommas()
        {
            var package = new Package
            {
                Version = "1.0.0",
                LicenseNames = "l1,l2, l3 ,l4  ,  l5 ",
            };
            var packageViewModel = new PackageViewModel(package);
            Assert.Equal(new string[] { "l1", "l2", "l3", "l4", "l5" }, packageViewModel.LicenseNames);
        }

        [Fact]
        public void LicenseReportFieldsKeptWhenLicenseReportDisabled()
        {
            var package = new Package
            {
                Version = "1.0.0",
                HideLicenseReport = true,
                LicenseNames = "l1",
                LicenseReportUrl = "url"
            };
            var packageViewModel = new PackageViewModel(package);
            Assert.NotNull(packageViewModel.LicenseNames);
            Assert.NotNull(packageViewModel.LicenseReportUrl);
        }

        [Fact]
        public void LicenseReportUrlKeptWhenLicenseReportEnabled()
        {
            var package = new Package
            {
                Version = "1.0.0",
                HideLicenseReport = false,
                LicenseReportUrl = "url"
            };
            var packageViewModel = new PackageViewModel(package);
            Assert.NotNull(packageViewModel.LicenseReportUrl);
        }

        [Fact]
        public void LicenseNamesKeptWhenLicenseReportEnabled()
        {
            var package = new Package
            {
                Version = "1.0.0",
                HideLicenseReport = false,
                LicenseNames = "l1"
            };
            var packageViewModel = new PackageViewModel(package);
            Assert.NotNull(packageViewModel.LicenseNames);
        }

        [Fact]
        public void LicenseUrlKeptWhenLicenseReportDisabled()
        {
            var package = new Package
            {
                Version = "1.0.0",
                HideLicenseReport = true,
                LicenseUrl = "url"
            };
            var packageViewModel = new PackageViewModel(package);
            Assert.NotNull(packageViewModel.LicenseUrl);
        }

        [Theory]
        [InlineData(PackageStatus.Available, true, PackageStatusSummary.Listed)]
        [InlineData(PackageStatus.Available, false, PackageStatusSummary.Unlisted)]
        [InlineData(PackageStatus.Deleted, true, PackageStatusSummary.None)]
        [InlineData(PackageStatus.Deleted, false, PackageStatusSummary.None)]
        [InlineData(PackageStatus.FailedValidation, true, PackageStatusSummary.FailedValidation)]
        [InlineData(PackageStatus.FailedValidation, false, PackageStatusSummary.FailedValidation)]
        [InlineData(PackageStatus.Validating, true, PackageStatusSummary.Validating)]
        [InlineData(PackageStatus.Validating, false, PackageStatusSummary.Validating)]
        public void PackageStatusSummaryIsCorrect(PackageStatus packageStatus, bool isListed, PackageStatusSummary expected)
        {
            // Arrange
            var package = new Package
            {
                Version = "1.0.0",
                PackageStatusKey = packageStatus,
                Listed = isListed
            };

            // Act 
            var packageViewModel = new PackageViewModel(package);

            // Assert
            Assert.Equal(expected, packageViewModel.PackageStatusSummary);
        }

        [Fact]
        public void PackageStatusSummaryThrowsForUnexpectedPackageStatus()
        {
            // Arrange
            var package = new Package
            {
                Version = "1.0.0",
                PackageStatusKey = (PackageStatus)4,
            };

            // Act 
            var packageViewModel = new PackageViewModel(package);

            // Assert
            Assert.Throws<ArgumentOutOfRangeException>(() => packageViewModel.PackageStatusSummary);
        }
    }
}
