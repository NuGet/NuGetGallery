// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using Moq;
using NuGet.Services.Entities;
using Xunit;

namespace NuGetGallery.ViewModels
{
    public class PackageViewModelFacts
    {
        [Fact]
        public void UsesVersionSpecificIdIfAvailable()
        {
            var package = new Package()
            {
                Version = "1.0.0",
                Id = "NuGet.IoT",
                PackageRegistration = new PackageRegistration { Id = "nugeT.ioT" },
            };
            var packageViewModel = CreatePackageViewModel(package);
            Assert.Equal("NuGet.IoT", packageViewModel.Id);
        }

        [Fact]
        public void UsesPackageRegistrationIdIfVersionSpecificIsNotAvailable()
        {
            var package = new Package
            {
                Version = "1.0.0",
                PackageRegistration = new PackageRegistration { Id = "nugeT.ioT" },
            };
            var packageViewModel = CreatePackageViewModel(package);
            Assert.Equal("nugeT.ioT", packageViewModel.Id);
        }

        [Fact]
        public void UsesNormalizedVersionForDisplay()
        {
            var package = new Package()
            {
                Version = "01.02.00.00",
                PackageRegistration = new PackageRegistration { Id = "SomeId" },
                NormalizedVersion = "1.3.0" // Different just to prove the View Model is using the DB column.
            };
            var packageViewModel = CreatePackageViewModel(package);
            Assert.Equal("1.3.0", packageViewModel.Version);
        }

        [Fact]
        public void UsesNormalizedPackageVersionIfNormalizedVersionMissing()
        {
            var package = new Package()
            {
                Version = "01.02.00.00",
                PackageRegistration = new PackageRegistration { Id = "SomeId" },
            };
            var packageViewModel = CreatePackageViewModel(package);
            Assert.Equal("1.2.0", packageViewModel.Version);
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
                PackageRegistration = new PackageRegistration { Id = "SomeId" },
                PackageStatusKey = packageStatus,
                Listed = isListed
            };

            // Act 
            var packageViewModel = CreatePackageViewModel(package);

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
                PackageRegistration = new PackageRegistration { Id = "SomeId" },
                PackageStatusKey = (PackageStatus)4,
            };

            // Act & Assert
            Assert.Throws<ArgumentOutOfRangeException>(() => CreatePackageViewModel(package));
        }

        [Fact]
        public void SetupExtensionUsesIconFromProvider()
        {
            // Arrange
            var package = new Package
            {
                Version = "1.0.0",
                PackageRegistration = new PackageRegistration { Id = "SomeId" },
            };

            var expectedUri = new Uri("https://nuget.test/icon");

            var iconUrlProviderMock = new Mock<IIconUrlProvider>();
            iconUrlProviderMock
                .Setup(iup => iup.GetIconUrlString(package))
                .Returns(expectedUri.AbsoluteUri);
            iconUrlProviderMock
                .Setup(iup => iup.GetIconUrl(package))
                .Returns(expectedUri);

            // Act
            var viewModel = CreatePackageViewModel(package, iconUrlProviderMock.Object);

            // Assert
            Assert.Equal(expectedUri.AbsoluteUri, viewModel.IconUrl);
        }

        private static PackageViewModel CreatePackageViewModel(Package package, IIconUrlProvider iconUrlProvider = null)
        {
            return new PackageViewModelFactory(iconUrlProvider ?? Mock.Of<IIconUrlProvider>()).Create(package);
        }
    }
}
