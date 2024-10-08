// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Linq;
using System.Collections.Generic;
using Moq;
using Xunit;
using NuGet.Services.Entities;
using NuGetGallery.Services;

namespace NuGetGallery
{
    public class TyposquattingServiceFacts
    {
        private static List<string> _packageIds = new List<string>
        {
            "microsoft_netframework_v1",
            "WindowsAzure.Caching",
            "SinglePageApplication",
            "PoliteCaptcha",
            "AspNetRazor.Core",
            "System.Json",
            "System.Spatial"
        };

        private static IQueryable<PackageRegistration> PackageRegistrationsList = Enumerable.Range(0, _packageIds.Count()).Select(i =>
                new PackageRegistration()
                {
                    Id = _packageIds[i],
                    DownloadCount = new Random().Next(0, 10000),
                    IsVerified = true,
                    Owners = new List<User> { new User() { Username = string.Format("owner{0}", i + 1), Key = i + 1 } }
                }).AsQueryable();

        private User _uploadedPackageOwner = new User() { Username = string.Format("owner{0}", _packageIds.Count() + 1), Key = _packageIds.Count() + 1 };

        private static ITyposquattingService CreateService(
            Mock<IPackageService> packageService = null,
            Mock<IContentObjectService> contentObjectService = null,
            Mock<IFeatureFlagService> featureFlagService = null,
            Mock<IReservedNamespaceService> reservedNamespaceService = null,
            Mock<ITelemetryService> telemetryService = null,
            Mock<ITyposquattingCheckListCacheService> typosquattingCheckListCacheService = null)
        {
            if (packageService == null)
            {
                packageService = new Mock<IPackageService>();
                packageService
                    .Setup(x => x.GetAllPackageRegistrations())
                    .Returns(PackageRegistrationsList);
            }

            if (contentObjectService == null)
            {
                contentObjectService = new Mock<IContentObjectService>();
                contentObjectService
                    .Setup(x => x.TyposquattingConfiguration.PackageIdChecklistLength)
                    .Returns(20000);
                contentObjectService
                    .Setup(x => x.TyposquattingConfiguration.PackageIdChecklistCacheExpireTimeInHours)
                    .Returns(24);
            }

            if (featureFlagService == null)
            {
                featureFlagService = new Mock<IFeatureFlagService>();
                featureFlagService
                    .Setup(f => f.IsTyposquattingEnabled())
                    .Returns(true);
                featureFlagService
                    .Setup(f => f.IsTyposquattingEnabled(It.IsAny<User>()))
                    .Returns(true);
            }

            if (reservedNamespaceService == null)
            {
                reservedNamespaceService = new Mock<IReservedNamespaceService>();
                reservedNamespaceService
                    .Setup(x => x.GetReservedNamespacesForId(It.IsAny<string>()))
                    .Returns(new List<ReservedNamespace>());
            }

            if (telemetryService == null)
            {
                telemetryService = new Mock<ITelemetryService>();
            }

            if (typosquattingCheckListCacheService == null)
            {
                List<NormalizedPackageIdInfo> normalizedPackageIdInfos = PackageRegistrationsList
                    .ToList()
                    .Select(pr => new NormalizedPackageIdInfo(pr.Id, pr.Id))
                    .ToList();

                typosquattingCheckListCacheService = new Mock<ITyposquattingCheckListCacheService>();
                typosquattingCheckListCacheService
                    .Setup(x => x.GetTyposquattingCheckList(It.IsAny<int>(), It.IsAny<TimeSpan>(), It.IsAny<IPackageService>()))
                    .Returns(normalizedPackageIdInfos);
            }

            return new TyposquattingService(
                contentObjectService.Object,
                featureFlagService.Object,
                packageService.Object,
                reservedNamespaceService.Object,
                telemetryService.Object,
                typosquattingCheckListCacheService.Object,
                new ExactMatchTyposquattingServiceHelper());
        }

        [Fact]
        public void CheckNotTyposquattingByDifferentOwnersTest()
        {
            // Arrange            
            var uploadedPackageId = "new_package_for_testing";

            var newService = CreateService();

            // Act
            var typosquattingCheckResult = newService.IsUploadedPackageIdTyposquatting(uploadedPackageId, _uploadedPackageOwner, out List<string> typosquattingCheckCollisionIds);

            // Assert
            Assert.False(typosquattingCheckResult);
            Assert.Empty(typosquattingCheckCollisionIds);
        }

        [Fact]
        public void CheckNotTyposquattingBySameOwnersTest()
        {
            // Arrange            
            _uploadedPackageOwner.Username = "owner1";
            _uploadedPackageOwner.Key = 1;
            var uploadedPackageId = "microsoft_netframework.v1";

            var newService = CreateService();

            // Act
            var typosquattingCheckResult = newService.IsUploadedPackageIdTyposquatting(uploadedPackageId, _uploadedPackageOwner, out List<string> typosquattingCheckCollisionIds);

            // Assert
            Assert.False(typosquattingCheckResult);
            Assert.Empty(typosquattingCheckCollisionIds);
        }

        [Fact]
        public void CheckNotTyposquattingWithinReservedNameSpace()
        {
            // Arrange
            var uploadedPackageId = "microsoft_netframework.v1";

            var mockReservedNamespaceService = new Mock<IReservedNamespaceService>();
            mockReservedNamespaceService
                .Setup(x => x.GetReservedNamespacesForId(It.IsAny<string>()))
                .Returns(new List<ReservedNamespace> { new ReservedNamespace() });

            var newService = CreateService(reservedNamespaceService: mockReservedNamespaceService);

            // Act
            var typosquattingCheckResult = newService.IsUploadedPackageIdTyposquatting(uploadedPackageId, _uploadedPackageOwner, out List<string> typosquattingCheckCollisionIds);

            // Assert
            Assert.False(typosquattingCheckResult);
            Assert.Empty(typosquattingCheckCollisionIds);
        }

        [Fact]
        public void CheckTyposquattingNullUploadedPackageId()
        {
            // Arrange
            string uploadedPackageId = null;

            var newService = CreateService();

            // Act
            var exception = Assert.Throws<ArgumentNullException>(
                () => newService.IsUploadedPackageIdTyposquatting(uploadedPackageId, _uploadedPackageOwner, out List<string> typosquattingCheckCollisionIds));

            // Assert
            Assert.Equal(nameof(uploadedPackageId), exception.ParamName);
        }

        [Fact]
        public void CheckTyposquattingNullUploadedPackageOwner()
        {
            // Arrange
            _uploadedPackageOwner = null;
            var uploadedPackageId = "microsoft_netframework_v1";

            var newService = CreateService();

            // Act
            var exception = Assert.Throws<ArgumentNullException>(
                () => newService.IsUploadedPackageIdTyposquatting(uploadedPackageId, _uploadedPackageOwner, out List<string> typosquattingCheckCollisionIds));

            // Assert
            Assert.Equal("uploadedPackageOwner", exception.ParamName);
        }

        [Fact]
        public void CheckTyposquattingEmptyUploadedPackageId()
        {
            // Arrange
            var uploadedPackageId = "";

            var newService = CreateService();

            // Act
            var typosquattingCheckResult = newService.IsUploadedPackageIdTyposquatting(uploadedPackageId, _uploadedPackageOwner, out List<string> typosquattingCheckCollisionIds);

            // Assert
            Assert.False(typosquattingCheckResult);
            Assert.Empty(typosquattingCheckCollisionIds);
        }

        [Fact]
        public void CheckTyposquattingEmptyChecklist()
        {
            // Arrange
            var uploadedPackageId = "microsoft_netframework_v1";
            var mockPackageService = new Mock<IPackageService>();
            mockPackageService
                .Setup(x => x.GetAllPackageRegistrations())
                .Returns(new List<PackageRegistration>().AsQueryable());
            var mockTyposquattingCheckListCacheService = new Mock<ITyposquattingCheckListCacheService>();
            mockTyposquattingCheckListCacheService
                .Setup(x => x.GetTyposquattingCheckList(It.IsAny<int>(), It.IsAny<TimeSpan>(), It.IsAny<IPackageService>()))
                .Returns(new List<NormalizedPackageIdInfo>());

            var newService = CreateService(packageService: mockPackageService, typosquattingCheckListCacheService: mockTyposquattingCheckListCacheService);

            // Act
            var typosquattingCheckResult = newService.IsUploadedPackageIdTyposquatting(uploadedPackageId, _uploadedPackageOwner, out List<string> typosquattingCheckCollisionIds);

            // Assert
            Assert.False(typosquattingCheckResult);
            Assert.Empty(typosquattingCheckCollisionIds);
        }

        [Theory]
        [InlineData("Microsoft_NetFramework_v1")]
        [InlineData("new_package_for_testing")]
        public void CheckTyposquattingNotEnabled(string packageId)
        {
            // Arrange
            var uploadedPackageId = packageId;

            var mockFeatureFlagService = new Mock<IFeatureFlagService>();
            mockFeatureFlagService
                .Setup(f => f.IsTyposquattingEnabled())
                .Returns(false);
            mockFeatureFlagService
                .Setup(f => f.IsTyposquattingEnabled(_uploadedPackageOwner))
                .Returns(true);

            var newService = CreateService(featureFlagService: mockFeatureFlagService);

            // Act
            var typosquattingCheckResult = newService.IsUploadedPackageIdTyposquatting(uploadedPackageId, _uploadedPackageOwner, out List<string> typosquattingCheckCollisionIds);

            // Assert
            Assert.False(typosquattingCheckResult);
            Assert.Empty(typosquattingCheckCollisionIds);

            mockFeatureFlagService
                .Verify(f => f.IsTyposquattingEnabled(), Times.Once);
            mockFeatureFlagService
                .Verify(f => f.IsTyposquattingEnabled(It.IsAny<User>()), Times.Never);
        }

        [Fact]
        public void CheckNotTyposquattingBlockUserNotEnabled()
        {
            // Arrange
            var uploadedPackageId = "new_package_for_testing";

            var mockContentObjectService = new Mock<IContentObjectService>();
            mockContentObjectService
                .Setup(x => x.TyposquattingConfiguration.PackageIdChecklistLength)
                .Returns(20000);

            var newService = CreateService(contentObjectService: mockContentObjectService);

            // Act
            var typosquattingCheckResult = newService.IsUploadedPackageIdTyposquatting(uploadedPackageId, _uploadedPackageOwner, out List<string> typosquattingCheckCollisionIds);

            // Assert
            Assert.False(typosquattingCheckResult);
            Assert.Empty(typosquattingCheckCollisionIds);
        }

        [Fact]
        public void CheckIsTyposquattingBlockUserNotEnabled()
        {
            // Arrange
            var uploadedPackageId = "Microsoft_NetFramework_v1";

            var featureFlagService = new Mock<IFeatureFlagService>();
            featureFlagService
                .Setup(f => f.IsTyposquattingEnabled())
                .Returns(true);
            featureFlagService
                .Setup(f => f.IsTyposquattingEnabled(_uploadedPackageOwner))
                .Returns(false);

            var newService = CreateService(featureFlagService: featureFlagService);

            // Act

            var typosquattingCheckResult = newService.IsUploadedPackageIdTyposquatting(uploadedPackageId, _uploadedPackageOwner, out List<string> typosquattingCheckCollisionIds);

            // Assert
            Assert.False(typosquattingCheckResult);
            Assert.Single(typosquattingCheckCollisionIds);
            Assert.Equal("microsoft_netframework_v1", typosquattingCheckCollisionIds[0]);

            featureFlagService
                .Verify(f => f.IsTyposquattingEnabled(), Times.Once);
            featureFlagService
                .Verify(f => f.IsTyposquattingEnabled(_uploadedPackageOwner), Times.Once);
        }

        [Fact]
        public void CheckIsTyposquattingBlockDifferentOwnersUploadBlocked()
        {
            // Arrange
            var uploadedPackageId = "Microsoft_NetFramework_v1";
            string conflictingPackageId = "microsoft_netframework_v1";
            User conflictingPackageOwner = PackageRegistrationsList
                .Single(pr => pr.Id == conflictingPackageId)
                .Owners
                .First();

            // Make sure they have different owners.
            Assert.NotEqual(_uploadedPackageOwner.Key, conflictingPackageOwner.Key);

            var featureFlagService = new Mock<IFeatureFlagService>();
            featureFlagService
                .Setup(f => f.IsTyposquattingEnabled())
                .Returns(true);
            featureFlagService
                .Setup(f => f.IsTyposquattingEnabled(_uploadedPackageOwner))
                .Returns(true);

            var newService = CreateService(featureFlagService: featureFlagService);

            // Act

            var typosquattingCheckResult = newService.IsUploadedPackageIdTyposquatting(uploadedPackageId, _uploadedPackageOwner, out List<string> typosquattingCheckCollisionIds);

            // Assert
            Assert.True(typosquattingCheckResult);
            Assert.Single(typosquattingCheckCollisionIds);
            Assert.Equal(conflictingPackageId, typosquattingCheckCollisionIds[0]);

            featureFlagService
                .Verify(f => f.IsTyposquattingEnabled(), Times.Once);
            featureFlagService
                .Verify(f => f.IsTyposquattingEnabled(_uploadedPackageOwner), Times.Once);
        }

        [Fact]
        public void CheckIsTyposquattingBlockSameOwnerUploadNotBlocked()
        {
            // Arrange
            var uploadedPackageId = "Microsoft_NetFramework_v1";
            string conflictingPackageId = "microsoft_netframework_v1";

            var featureFlagService = new Mock<IFeatureFlagService>();

            // Use same owner for both packages.
            User samePackageOwner =  PackageRegistrationsList
                .Single(pr => pr.Id == conflictingPackageId)
                .Owners
                .First();

            featureFlagService
                .Setup(f => f.IsTyposquattingEnabled())
                .Returns(true);
            featureFlagService
                .Setup(f => f.IsTyposquattingEnabled(samePackageOwner))
                .Returns(true);

            var newService = CreateService(featureFlagService: featureFlagService);

            // Act

            var typosquattingCheckResult = newService.IsUploadedPackageIdTyposquatting(uploadedPackageId, samePackageOwner, out List<string> typosquattingCheckCollisionIds);

            // Assert
            Assert.False(typosquattingCheckResult);
            Assert.Empty(typosquattingCheckCollisionIds);

            featureFlagService
                .Verify(f => f.IsTyposquattingEnabled(), Times.Once);
            featureFlagService
                .Verify(f => f.IsTyposquattingEnabled(samePackageOwner), Times.Once);
        }

        [Fact]
        public void CheckTelemetryServiceLogOriginalUploadedPackageId()
        {
            // Arrange
            var uploadedPackageId = "microsoft_netframework.v1";
            var mockTelemetryService = new Mock<ITelemetryService>();

            var newService = CreateService(telemetryService: mockTelemetryService);

            // Act
            var typosquattingCheckResult = newService.IsUploadedPackageIdTyposquatting(uploadedPackageId, _uploadedPackageOwner, out List<string> typosquattingCheckCollisionIds);

            // Assert
            mockTelemetryService.Verify(
                x => x.TrackMetricForTyposquattingChecklistRetrievalTime(uploadedPackageId, It.IsAny<TimeSpan>()),
                Times.Once);

            mockTelemetryService.Verify(
                x => x.TrackMetricForTyposquattingAlgorithmProcessingTime(uploadedPackageId, It.IsAny<TimeSpan>()),
                Times.Once);

            mockTelemetryService.Verify(
                x => x.TrackMetricForTyposquattingCheckResultAndTotalTime(
                    uploadedPackageId,
                    It.IsAny<TimeSpan>(),
                    It.IsAny<bool>(),
                    It.IsAny<List<string>>(),
                    It.IsAny<int>(),
                    It.IsAny<TimeSpan>()),
                Times.Once);
        }
    }
}
