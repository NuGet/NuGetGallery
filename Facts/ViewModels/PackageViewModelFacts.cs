using System;
using System.Linq;
using System.Collections.Generic;
using Xunit;

namespace NuGetGallery.ViewModels
{
    public class PackageViewModelFacts
    {
        [Fact]
        public void LicenseNamesAreParsedByCommas()
        {
            var package = new Package
            {
                LicenseNames = "l1,l2, l3 ,l4  ,  l5 ",
            };
            var licenseNames = (new PackageViewModel(package)).LicenseNames;
            Assert.Contains("l1", licenseNames);
            Assert.Contains("l2", licenseNames);
            Assert.Contains("l3", licenseNames);
            Assert.Contains("l4", licenseNames);
            Assert.Contains("l5", licenseNames);
            Assert.Equal(5, licenseNames.Count());
        }

        [Fact]
        public void LicenseReportFieldsNullWhenLicenseReportDisabled()
        {
            var package = new Package
            {
                HideLicenseReport = true,
                LicenseNames = "l1",
                LicenseReportUrl = "url" 
            };
            var packageViewModel = new PackageViewModel(package);
            Assert.Null(packageViewModel.LicenseNames);
            Assert.Null(packageViewModel.LicenseReportUrl);
        }

        [Fact]
        public void LicenseReportUrlKeptWhenLicenseReportEnabled()
        {
            var package = new Package
            {
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
                HideLicenseReport = false,
                LicenseNames = "l1" 
            };
            var packageViewModel = new PackageViewModel(package);
            Assert.NotNull(packageViewModel.LicenseNames);
        }
    }
}
