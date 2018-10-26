// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Moq;
using NuGet.Services.Entities;
using Xunit;

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

        private static IQueryable<PackageRegistration> PacakgeRegistrationsList = Enumerable.Range(0, _packageIds.Count()).Select(i =>
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
            Mock<IReservedNamespaceService> reservedNamespaceService = null,
            Mock<ITelemetryService> telemetryService = null,
            TyposquattingCheckListCache typosquattingCheckListCache = null)
        {
            if (packageService == null)
            {
                packageService = new Mock<IPackageService>();
                packageService
                .Setup(x => x.GetAllPackageRegistrations())
                .Returns(PacakgeRegistrationsList);
            }

            if (contentObjectService == null)
            {
                contentObjectService = new Mock<IContentObjectService>();
                contentObjectService
                 .Setup(x => x.TyposquattingConfiguration.PackageIdChecklistLength)
                 .Returns(20000);
                contentObjectService
                .Setup(x => x.TyposquattingConfiguration.IsCheckEnabled)
                .Returns(true);
                contentObjectService
                    .Setup(x => x.TyposquattingConfiguration.IsBlockUsersEnabled)
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

            if (typosquattingCheckListCache == null)
            {
                typosquattingCheckListCache = new TyposquattingCheckListCache();
            }

            return new TyposquattingService(contentObjectService.Object, packageService.Object, reservedNamespaceService.Object, telemetryService.Object, typosquattingCheckListCache); ;
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
            Assert.Equal(0, typosquattingCheckCollisionIds.Count);
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
            Assert.Equal(0, typosquattingCheckCollisionIds.Count);
        }

        [Fact]
        public void CheckIsTyposquattingByDifferentOwnersTest()
        {
            // Arrange            
            var uploadedPackageId = "Mícrosoft.NetFramew0rk.v1";
            var newService = CreateService();
            
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
            var pacakgeRegistrationsList = PacakgeRegistrationsList.Concat(new PackageRegistration[]
            {
                new PackageRegistration {
                    Id = "microsoft-netframework-v1",
                    DownloadCount = new Random().Next(0, 10000),
                    IsVerified = true,
                    Owners = new List<User> { new User() { Username = string.Format("owner{0}", _packageIds.Count() + 2), Key = _packageIds.Count() + 2} }
                }
            });
            var mockPackageService = new Mock<IPackageService>();
            mockPackageService
               .Setup(x => x.GetAllPackageRegistrations())
               .Returns(pacakgeRegistrationsList);

            var newService = CreateService(packageService: mockPackageService);

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
            var pacakgeRegistrationsList = PacakgeRegistrationsList.Concat(new PackageRegistration[]
            {
                new PackageRegistration()
                {
                    Id = "microsoft-netframework-v1",
                    DownloadCount = new Random().Next(0, 10000),
                    IsVerified = true,
                    Owners = new List<User> { new User() { Username = string.Format("owner{0}", _packageIds.Count() + 2), Key = _packageIds.Count() + 2 } }
                }
            });

            var mockPackageService = new Mock<IPackageService>();
            mockPackageService
               .Setup(x => x.GetAllPackageRegistrations())
               .Returns(pacakgeRegistrationsList);

            var newService = CreateService(packageService: mockPackageService);

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

            var mockReservedNamespaceService = new Mock<IReservedNamespaceService>();
            mockReservedNamespaceService
                .Setup(x => x.GetReservedNamespacesForId(It.IsAny<string>()))
                .Returns(new List<ReservedNamespace> { new ReservedNamespace()});

            var newService = CreateService(reservedNamespaceService: mockReservedNamespaceService);

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
            Assert.Equal(0, typosquattingCheckCollisionIds.Count);
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

            var newService = CreateService(packageService: mockPackageService);

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

            var mockContentObjectService = new Mock<IContentObjectService>();
            mockContentObjectService
             .Setup(x => x.TyposquattingConfiguration.PackageIdChecklistLength)
             .Returns(20000);
            mockContentObjectService
                 .Setup(x => x.TyposquattingConfiguration.IsCheckEnabled)
                .Returns(false);
            mockContentObjectService
                .Setup(x => x.TyposquattingConfiguration.IsBlockUsersEnabled)
                .Returns(true);

            var newService = CreateService(contentObjectService: mockContentObjectService);

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

            var mockContentObjectService = new Mock<IContentObjectService>();
            mockContentObjectService
             .Setup(x => x.TyposquattingConfiguration.PackageIdChecklistLength)
             .Returns(20000);
            mockContentObjectService
                 .Setup(x => x.TyposquattingConfiguration.IsCheckEnabled)
                .Returns(false);
            mockContentObjectService
                .Setup(x => x.TyposquattingConfiguration.IsBlockUsersEnabled)
                .Returns(false);

            var newService = CreateService(contentObjectService: mockContentObjectService);
            
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
            var mockContentObjectService = new Mock<IContentObjectService>();
            mockContentObjectService
             .Setup(x => x.TyposquattingConfiguration.PackageIdChecklistLength)
             .Returns(20000);
            mockContentObjectService
                 .Setup(x => x.TyposquattingConfiguration.IsCheckEnabled)
                .Returns(true);
            mockContentObjectService
                .Setup(x => x.TyposquattingConfiguration.IsBlockUsersEnabled)
                .Returns(false);

            var newService = CreateService(contentObjectService: mockContentObjectService);
            
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
                    It.IsAny<int>()),
                Times.Once);

            mockTelemetryService.Verify(
                x => x.TrackMetricForTyposquattingOwnersCheckTime(uploadedPackageId, It.IsAny<TimeSpan>()),
                Times.Once);
        }

        [Fact]
        public void CheckTyposquattingChecklistCache()
        {
            // Arrange
            var uploadedPackageId = "new_package_for_testing";
            var mockPackageService = new Mock<IPackageService>();
            mockPackageService
                .Setup(x => x.GetAllPackageRegistrations())
                .Returns(PacakgeRegistrationsList);

            var newService = CreateService(packageService: mockPackageService);

            int tasksNum = 3;
            Task[] tasks = new Task[tasksNum];
            for (int i = 0; i < tasksNum; i++)
            {
                tasks[i] = Task.Factory.StartNew(() =>
                {
                    newService.IsUploadedPackageIdTyposquatting(uploadedPackageId, _uploadedPackageOwner, out List<string> typosquattingCheckCollisionIds);
                });
            }
            Task.WaitAll(tasks);

            mockPackageService.Verify(
               x => x.GetAllPackageRegistrations(),
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