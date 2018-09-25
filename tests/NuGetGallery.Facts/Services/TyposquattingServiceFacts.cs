// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using Moq;
using Xunit;

namespace NuGetGallery
{
    public class TyposquattingServiceFacts
    {
        private static Mock<IPackageService> _packageService = new Mock<IPackageService>();
        private static Mock<IContentObjectService> _contentObjectService = new Mock<IContentObjectService>();
        private static Mock<IReservedNamespaceService> _reservedNamespaceService = new Mock<IReservedNamespaceService>();
        private static Mock<ITelemetryService> _telemetryService = new Mock<ITelemetryService>();

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

        private IQueryable<PackageRegistration> _pacakgeRegistrationsList = Enumerable.Range(0, _packageIds.Count()).Select(i =>
                new PackageRegistration()
                {
                    Id = _packageIds[i],
                    DownloadCount = new Random().Next(0, 10000),
                    IsVerified = true,
                    Owners = new List<User> { new User() { Username = string.Format("owner{0}", i + 1), Key = i + 1 } }
                }).AsQueryable();

        private User _uploadedPackageOwner = new User() { Username = string.Format("owner{0}", _packageIds.Count() + 1), Key = _packageIds.Count() + 1 };

        public TyposquattingServiceFacts()
        {
            _packageService
                .Setup(x => x.GetAllPackageRegistrations())
                .Returns(_pacakgeRegistrationsList);

            _contentObjectService
                .Setup(x => x.TyposquattingConfiguration.PackageIdChecklistLength)
                .Returns(20000);

            _contentObjectService
                .Setup(x => x.TyposquattingConfiguration.IsCheckEnabled)
                .Returns(true);

            _contentObjectService
                .Setup(x => x.TyposquattingConfiguration.IsBlockUsersEnabled)
                .Returns(true);

            _reservedNamespaceService
                .Setup(x => x.GetReservedNamespacesForId(It.IsAny<string>()))
                .Returns(new List<ReservedNamespace>());
        }

        [Fact]
        public void CheckNotTyposquattingByDifferentOwnersTest()
        {
            // Arrange            
            var uploadedPackageId = "new_package_for_testing";
            var newService = new TyposquattingService(_contentObjectService.Object, _packageService.Object, _reservedNamespaceService.Object, _telemetryService.Object);

            // Act
            var typosquattingCheckResult = newService.IsUploadedPackageIdTyposquatting(uploadedPackageId, _uploadedPackageOwner, out List<string> typosquattingCheckCollisionIds);

            // Assert
            Assert.False(typosquattingCheckResult);
            Assert.Equal(0, typosquattingCheckCollisionIds.Count);
        }

        [Fact]
        public void CheckNotTyposquattingBySameOwnersTest()
        {
            // Arrange            
            _uploadedPackageOwner.Username = "owner1";
            _uploadedPackageOwner.Key = 1;
            var uploadedPackageId = "microsoft_netframework.v1";

            var newService = new TyposquattingService(_contentObjectService.Object, _packageService.Object, _reservedNamespaceService.Object, _telemetryService.Object);

            // Act
            var typosquattingCheckResult = newService.IsUploadedPackageIdTyposquatting(uploadedPackageId, _uploadedPackageOwner, out List<string> typosquattingCheckCollisionIds);

            // Assert
            Assert.False(typosquattingCheckResult);
            Assert.Equal(0, typosquattingCheckCollisionIds.Count);
        }

        [Fact]
        public void CheckIsTyposquattingByDifferentOwnersTest()
        {
            // Arrange            
            var uploadedPackageId = "Mícrosoft.NetFramew0rk.v1";
            var newService = new TyposquattingService(_contentObjectService.Object, _packageService.Object, _reservedNamespaceService.Object, _telemetryService.Object);

            // Act
            var typosquattingCheckResult = newService.IsUploadedPackageIdTyposquatting(uploadedPackageId, _uploadedPackageOwner, out List<string> typosquattingCheckCollisionIds);

            // Assert
            Assert.True(typosquattingCheckResult);
            Assert.Equal(1, typosquattingCheckCollisionIds.Count);
            Assert.Equal("microsoft_netframework_v1", typosquattingCheckCollisionIds[0]);
        }


        [Fact]
        public void CheckIsTyposquattingMultiCollisionsWithoutSameUser()
        {
            // Arrange
            var uploadedPackageId = "microsoft_netframework.v1";
            _pacakgeRegistrationsList = _pacakgeRegistrationsList.Concat(new PackageRegistration[]
            {
                new PackageRegistration {
                    Id = "microsoft-netframework-v1",
                    DownloadCount = new Random().Next(0, 10000),
                    IsVerified = true,
                    Owners = new List<User> { new User() { Username = string.Format("owner{0}", _packageIds.Count() + 2), Key = _packageIds.Count() + 2} }
                }
            });
            _packageService
               .Setup(x => x.GetAllPackageRegistrations())
               .Returns(_pacakgeRegistrationsList);

            var newService = new TyposquattingService(_contentObjectService.Object, _packageService.Object, _reservedNamespaceService.Object, _telemetryService.Object);

            // Act
            var typosquattingCheckResult = newService.IsUploadedPackageIdTyposquatting(uploadedPackageId, _uploadedPackageOwner, out List<string> typosquattingCheckCollisionIds);

            // Assert
            Assert.True(typosquattingCheckResult);
            Assert.Equal(2, typosquattingCheckCollisionIds.Count);
        }

        [Fact]
        public void CheckNotTyposquattingMultiCollisionsWithSameUsers()
        {
            // Arrange            
            var uploadedPackageId = "microsoft_netframework.v1";
            _uploadedPackageOwner.Username = "owner1";
            _uploadedPackageOwner.Key = 1;
            _pacakgeRegistrationsList = _pacakgeRegistrationsList.Concat(new PackageRegistration[]
            {
                new PackageRegistration()
                {
                    Id = "microsoft-netframework-v1",
                    DownloadCount = new Random().Next(0, 10000),
                    IsVerified = true,
                    Owners = new List<User> { new User() { Username = string.Format("owner{0}", _packageIds.Count() + 2), Key = _packageIds.Count() + 2 } }
                }
            });
            _packageService
               .Setup(x => x.GetAllPackageRegistrations())
               .Returns(_pacakgeRegistrationsList);

            var newService = new TyposquattingService(_contentObjectService.Object, _packageService.Object, _reservedNamespaceService.Object, _telemetryService.Object);

            // Act
            var typosquattingCheckResult = newService.IsUploadedPackageIdTyposquatting(uploadedPackageId, _uploadedPackageOwner, out List<string> typosquattingCheckCollisionIds);

            // Assert
            Assert.False(typosquattingCheckResult);
            Assert.Equal(1, typosquattingCheckCollisionIds.Count);
            Assert.Equal("microsoft-netframework-v1", typosquattingCheckCollisionIds[0]);
        }

        [Fact]
        public void CheckNotTyposquattingWithinReservedNameSpace()
        {
            // Arrange
            var uploadedPackageId = "microsoft_netframework.v1";
            _reservedNamespaceService
                .Setup(x => x.GetReservedNamespacesForId(It.IsAny<string>()))
                .Returns(new List<ReservedNamespace> { new ReservedNamespace()});

            var newService = new TyposquattingService(_contentObjectService.Object, _packageService.Object, _reservedNamespaceService.Object, _telemetryService.Object);

            // Act
            var typosquattingCheckResult = newService.IsUploadedPackageIdTyposquatting(uploadedPackageId, _uploadedPackageOwner, out List<string> typosquattingCheckCollisionIds);

            // Assert
            Assert.False(typosquattingCheckResult);
            Assert.Equal(0, typosquattingCheckCollisionIds.Count);
        }

        [Fact]
        public void CheckTyposquattingNullUploadedPackageId()
        {
            // Arrange
            string uploadedPackageId = null;

            var newService = new TyposquattingService(_contentObjectService.Object, _packageService.Object, _reservedNamespaceService.Object, _telemetryService.Object);

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

            var newService = new TyposquattingService(_contentObjectService.Object, _packageService.Object, _reservedNamespaceService.Object, _telemetryService.Object);

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

            var newService = new TyposquattingService(_contentObjectService.Object, _packageService.Object, _reservedNamespaceService.Object, _telemetryService.Object);

            // Act
            var typosquattingCheckResult = newService.IsUploadedPackageIdTyposquatting(uploadedPackageId, _uploadedPackageOwner, out List<string> typosquattingCheckCollisionIds);

            // Assert
            Assert.False(typosquattingCheckResult);
            Assert.Equal(0, typosquattingCheckCollisionIds.Count);
        }

        [Fact]
        public void CheckTyposquattingEmptyChecklist()
        {
            // Arrange
            var uploadedPackageId = "microsoft_netframework_v1";
            _packageService
                .Setup(x => x.GetAllPackageRegistrations())
                .Returns(new List<PackageRegistration>().AsQueryable());

            var newService = new TyposquattingService(_contentObjectService.Object, _packageService.Object, _reservedNamespaceService.Object, _telemetryService.Object);

            // Act
            var typosquattingCheckResult = newService.IsUploadedPackageIdTyposquatting(uploadedPackageId, _uploadedPackageOwner, out List<string> typosquattingCheckCollisionIds);

            // Assert
            Assert.False(typosquattingCheckResult);
            Assert.Equal(0, typosquattingCheckCollisionIds.Count);
        }

        [Theory]
        [InlineData("Microsoft_NetFramework_v1")]
        [InlineData("new_package_for_testing")]
        public void CheckTyposquattingNotEnabled(string packageId)
        {
            // Arrange
            var uploadedPackageId = packageId;
            _contentObjectService
                .Setup(x => x.TyposquattingConfiguration.IsCheckEnabled)
                .Returns(false);

            var newService = new TyposquattingService(_contentObjectService.Object, _packageService.Object, _reservedNamespaceService.Object, _telemetryService.Object);

            // Act
            var typosquattingCheckResult = newService.IsUploadedPackageIdTyposquatting(uploadedPackageId, _uploadedPackageOwner, out List<string> typosquattingCheckCollisionIds);

            // Assert
            Assert.False(typosquattingCheckResult);
            Assert.Equal(0, typosquattingCheckCollisionIds.Count);
        }

        [Fact]
        public void CheckNotTyposquattingBlockUserNotEnabled()
        {
            // Arrange
            var uploadedPackageId = "new_package_for_testing";
            _contentObjectService
                .Setup(x => x.TyposquattingConfiguration.IsBlockUsersEnabled)
                .Returns(false);

            var newService = new TyposquattingService(_contentObjectService.Object, _packageService.Object, _reservedNamespaceService.Object, _telemetryService.Object);

            // Act
            var typosquattingCheckResult = newService.IsUploadedPackageIdTyposquatting(uploadedPackageId, _uploadedPackageOwner, out List<string> typosquattingCheckCollisionIds);

            // Assert
            Assert.False(typosquattingCheckResult);
            Assert.Equal(0, typosquattingCheckCollisionIds.Count);
        }

        [Fact]
        public void CheckIsTyposquattingBlockUserNotEnabled()
        {
            // Arrange
            var uploadedPackageId = "Microsoft_NetFramework_v1";
            _contentObjectService
                .Setup(x => x.TyposquattingConfiguration.IsBlockUsersEnabled)
                .Returns(false);

            var newService = new TyposquattingService(_contentObjectService.Object, _packageService.Object, _reservedNamespaceService.Object, _telemetryService.Object);

            // Act
            var typosquattingCheckResult = newService.IsUploadedPackageIdTyposquatting(uploadedPackageId, _uploadedPackageOwner, out List<string> typosquattingCheckCollisionIds);

            // Assert
            Assert.False(typosquattingCheckResult);
            Assert.Equal(1, typosquattingCheckCollisionIds.Count);
            Assert.Equal("microsoft_netframework_v1", typosquattingCheckCollisionIds[0]);
        }

        [Fact]
        public void CheckTelemetryServiceLogOriginalUploadedPackageId()
        {
            // Arrange
            var uploadedPackageId = "microsoft_netframework.v1";
            var newService = new TyposquattingService(_contentObjectService.Object, _packageService.Object, _reservedNamespaceService.Object, _telemetryService.Object);

            // Act
            var typosquattingCheckResult = newService.IsUploadedPackageIdTyposquatting(uploadedPackageId, _uploadedPackageOwner, out List<string> typosquattingCheckCollisionIds);

            // Assert
            _telemetryService.Verify(
                x => x.TrackMetricForTyposquattingChecklistRetrievalTime(uploadedPackageId, It.IsAny<TimeSpan>()),
                Times.Once);

            _telemetryService.Verify(
                x => x.TrackMetricForTyposquattingAlgorithmProcessingTime(uploadedPackageId, It.IsAny<TimeSpan>()),
                Times.Once);

            _telemetryService.Verify(
                x => x.TrackMetricForTyposquattingCheckResultAndTotalTime(
                    uploadedPackageId,
                    It.IsAny<TimeSpan>(),
                    It.IsAny<bool>(),
                    It.IsAny<List<string>>(),
                    It.IsAny<int>()),
                Times.Once);

            _telemetryService.Verify(
                x => x.TrackMetricForTyposquattingOwnersCheckTime(uploadedPackageId, It.IsAny<TimeSpan>()),
                Times.Once);
        }
        
        [Theory]
        [InlineData("Microsoft_NetFramework_v1", "Microsoft.NetFramework.v1", 0)]
        [InlineData("Microsoft_NetFramework_v1", "microsoft-netframework-v1", 0)]
        [InlineData("Microsoft_NetFramework_v1", "MicrosoftNetFrameworkV1", 0)]
        [InlineData("Microsoft_NetFramework_v1", "Mícr0s0ft_NetFrάmѐw0rk_v1", 0)]
        [InlineData("Dotnet.Script.Core.RoslynDependencies", "dotnet-script-core-rõslyndependencies", 1)]
        [InlineData("Dotnet.Script.Core.RoslynDependencies", "DotnetScriptCoreRoslyndependncies", 1)]
        [InlineData("MichaelBrandonMorris.Extensions.CollectionExtensions", "Michaelbrandonmorris.Extension.CollectionExtension", 2)]
        [InlineData("MichaelBrandonMorris.Extensions.CollectionExtensions", "MichaelBrandonMoris_Extensions_CollectionExtension", 2)]
        public void CheckTyposquattingDistance(string str1, string str2, int threshold)
        {
            // Arrange 
            str1 = TyposquattingStringNormalization.NormalizeString(str1);
            str2 = TyposquattingStringNormalization.NormalizeString(str2);

            // Act
            var checkResult = TyposquattingDistanceCalculation.IsDistanceLessThanThreshold(str1, str2, threshold);
            
            // Assert
            Assert.True(checkResult);
        }

        [Theory]
        [InlineData("Lappa.ORM", "JCTools.I18N", 0)]
        [InlineData("Cake.Intellisense.Core", "Cake.IntellisenseGenerator", 0)]
        [InlineData("Hangfire.Net40", "Hangfire.SqlServer.Net40", 0)]
        [InlineData("LogoFX.Client.Tests.Integration.SpecFlow.Core", "LogoFX.Client.Testing.EndToEnd.SpecFlow", 1)]
        [InlineData("cordova-plugin-ms-adal.TypeScript.DefinitelyTyped", "eonasdan-bootstrap-datetimepicker.TypeScript.DefinitelyTyped", 2)]
        public void CheckNotTyposquattingDistance(string str1, string str2, int threshold)
        {
            // Arrange
            str1 = TyposquattingStringNormalization.NormalizeString(str1);
            str2 = TyposquattingStringNormalization.NormalizeString(str2);

            // Act
            var checkResult = TyposquattingDistanceCalculation.IsDistanceLessThanThreshold(str1, str2, threshold);
            
            // Assert
            Assert.False(checkResult);
        }
        
        [Theory]
        [InlineData("Microsoft_NetFramework_v1", "microsoft_netframework_v1")]
        [InlineData("Microsoft.netframework-v1", "microsoft_netframework_v1")]
        [InlineData("mícr0s0ft.nёtFrǎmȇwὀrk.v1", "microsoft_netframework_v1")]
        public void CheckNormalization(string str1, string str2)
        {
            // Arrange and Act
            str1 = TyposquattingStringNormalization.NormalizeString(str1);

            // Assert
            Assert.Equal(str1, str2);
        }
    }
}