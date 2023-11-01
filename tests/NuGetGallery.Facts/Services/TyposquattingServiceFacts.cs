// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Linq;
using System.Globalization;
using System.Collections.Generic;
using Moq;
using Xunit;
using NuGet.Services.Entities;
using Microsoft.Extensions.Logging;
using System.Threading.Tasks;
using ICSharpCode.SharpZipLib.Zip;
using NuGetGallery.Cookies;

namespace NuGetGallery
{
    public class TyposquattingServiceFacts
    {
        [Fact]
        public void InitializeTyposquattingService_ThrowsIfTyposquattingServiceNull()
        {
            // Arrange, Act & Assert
            var exception = Assert.Throws<ArgumentNullException>(() => TyposquattingService.Initialize(null, It.IsAny<ILogger>()));
            Assert.Equal("typosquattingService", exception.ParamName);
        }

        [Fact]
        public void InitializeTyposquattingService_ThrowsIfLoggerNull()
        {
            // Arrange, Act & Assert
            var exception = Assert.Throws<ArgumentNullException>(() => TyposquattingService.Initialize(Mock.Of<ITyposquattingService>(), null));
            Assert.Equal("logger", exception.ParamName);
        }

        //private static List<string> _packageIds = new List<string>
        //{
        //    "microsoft_netframework_v1",
        //    "WindowsAzure.Caching",
        //    "SinglePageApplication",
        //    "PoliteCaptcha",
        //    "AspNetRazor.Core",
        //    "System.Json",
        //    "System.Spatial"
        //};

        //private static IQueryable<PackageRegistration> PackageRegistrationsList = Enumerable.Range(0, _packageIds.Count()).Select(i =>
        //        new PackageRegistration()
        //        {
        //            Id = _packageIds[i],
        //            DownloadCount = new Random().Next(0, 10000),
        //            IsVerified = true,
        //            Owners = new List<User> { new User() { Username = string.Format("owner{0}", i + 1), Key = i + 1 } }
        //        }).AsQueryable();

        //private User _uploadedPackageOwner = new User() { Username = string.Format("owner{0}", _packageIds.Count() + 1), Key = _packageIds.Count() + 1 };

        //private static ITyposquattingService CreateService(
        //    Mock<IPackageService> packageService = null,
        //    Mock<IContentObjectService> contentObjectService = null,
        //    Mock<IFeatureFlagService> featureFlagService = null,
        //    Mock<IReservedNamespaceService> reservedNamespaceService = null,
        //    Mock<ITelemetryService> telemetryService = null,
        //    Mock<ITyposquattingCheckListCacheService> typosquattingCheckListCacheService = null,
        //    Mock<ILogger> logger = null)
        //{
        //    if (packageService == null)
        //    {
        //        packageService = new Mock<IPackageService>();
        //        packageService
        //            .Setup(x => x.GetAllPackageRegistrations())
        //            .Returns(PackageRegistrationsList);
        //    }

        //    if (contentObjectService == null)
        //    {
        //        contentObjectService = new Mock<IContentObjectService>();
        //        contentObjectService
        //            .Setup(x => x.TyposquattingConfiguration.PackageIdChecklistLength)
        //            .Returns(20000);
        //        contentObjectService
        //            .Setup(x => x.TyposquattingConfiguration.PackageIdChecklistCacheExpireTimeInHours)
        //            .Returns(24);
        //    }

        //    if (featureFlagService == null)
        //    {
        //        featureFlagService = new Mock<IFeatureFlagService>();
        //        featureFlagService
        //            .Setup(f => f.IsTyposquattingEnabled())
        //            .Returns(true);
        //        featureFlagService
        //            .Setup(f => f.IsTyposquattingEnabled(It.IsAny<User>()))
        //            .Returns(true);
        //    }

        //    if (reservedNamespaceService == null)
        //    {
        //        reservedNamespaceService = new Mock<IReservedNamespaceService>();
        //        reservedNamespaceService
        //            .Setup(x => x.GetReservedNamespacesForId(It.IsAny<string>()))
        //            .Returns(new List<ReservedNamespace>());
        //    }

        //    if (telemetryService == null)
        //    {
        //        telemetryService = new Mock<ITelemetryService>();
        //    }

        //    if (typosquattingCheckListCacheService == null)
        //    {
        //        typosquattingCheckListCacheService = new Mock<ITyposquattingCheckListCacheService>();
        //        typosquattingCheckListCacheService
        //            .Setup(x => x.GetTyposquattingCheckList(It.IsAny<int>(), It.IsAny<TimeSpan>(), It.IsAny<IPackageService>()))
        //            .Returns(PackageRegistrationsList.Select(pr => pr.Id).ToList());
        //    }

        //    TyposquattingService.Initialize(new TestTyposquattingService(
        //                                        contentObjectService.Object,
        //                                        featureFlagService.Object,
        //                                        packageService.Object,
        //                                        reservedNamespaceService.Object,
        //                                        telemetryService.Object,
        //                                        typosquattingCheckListCacheService.Object),
        //                                        logger.Object);
        //    return TyposquattingService.Instance;
        //}

        //[Fact]
        //public async Task CheckNotTyposquattingByDifferentOwnersTestAsync()
        //{
        //    // Arrange            
        //    var uploadedPackageId = "new_package_for_testing";

        //    var newService = CreateService();

        //    // Act
        //    TyposquattingCheckResult typosquattingCheckResult = await newService.IsUploadedPackageIdTyposquattingAsync(GetMockTyposquattingCheckInfo(uploadedPackageId, _uploadedPackageOwner));

        //    // Assert
        //    Assert.False(typosquattingCheckResult.WasUploadBlocked);
        //    Assert.Equal(0, typosquattingCheckResult.TyposquattingCheckCollisionIds.Count());
        //}

        //[Fact]
        //public async void CheckNotTyposquattingBySameOwnersTest()
        //{
        //    // Arrange            
        //    _uploadedPackageOwner.Username = "owner1";
        //    _uploadedPackageOwner.Key = 1;
        //    var uploadedPackageId = "microsoft_netframework.v1";

        //    var newService = CreateService();

        //    // Act
        //    var typosquattingCheckResult = await newService.IsUploadedPackageIdTyposquattingAsync(GetMockTyposquattingCheckInfo(uploadedPackageId, _uploadedPackageOwner));

        //    // Assert
        //    Assert.False(typosquattingCheckResult.WasUploadBlocked);
        //    Assert.Equal(0, typosquattingCheckResult.TyposquattingCheckCollisionIds.Count());
        //}

        //[Fact]
        //public async Task CheckIsTyposquattingByDifferentOwnersTestAsync()
        //{
        //    // Arrange            
        //    var uploadedPackageId = "Mícrosoft.NetFramew0rk.v1";
        //    var newService = CreateService();

        //    // Act
        //    var typosquattingCheckResult = await newService.IsUploadedPackageIdTyposquattingAsync(GetMockTyposquattingCheckInfo(uploadedPackageId, _uploadedPackageOwner));

        //    // Assert
        //    Assert.True(typosquattingCheckResult.WasUploadBlocked);
        //    Assert.Equal(1, typosquattingCheckResult.TyposquattingCheckCollisionIds.Count());
        //    Assert.Equal("microsoft_netframework_v1", typosquattingCheckResult.TyposquattingCheckCollisionIds.First());
        //}

        //[Fact]
        //public async void CheckIsTyposquattingMultiCollisionsWithoutSameUser()
        //{
        //    // Arrange
        //    var uploadedPackageId = "microsoft_netframework.v1";
        //    var pacakgeRegistrationsList = PackageRegistrationsList.Concat(new PackageRegistration[]
        //    {
        //        new PackageRegistration {
        //            Id = "microsoft-netframework-v1",
        //            DownloadCount = new Random().Next(0, 10000),
        //            IsVerified = true,
        //            Owners = new List<User> { new User() { Username = string.Format("owner{0}", _packageIds.Count() + 2), Key = _packageIds.Count() + 2} }
        //        }
        //    });
        //    var mockPackageService = new Mock<IPackageService>();
        //    mockPackageService
        //        .Setup(x => x.GetAllPackageRegistrations())
        //        .Returns(pacakgeRegistrationsList);
        //    var mockTyposquattingCheckListCacheService = new Mock<ITyposquattingCheckListCacheService>();
        //    mockTyposquattingCheckListCacheService
        //        .Setup(x => x.GetTyposquattingCheckList(It.IsAny<int>(), It.IsAny<TimeSpan>(), It.IsAny<IPackageService>()))
        //        .Returns(pacakgeRegistrationsList.Select(pr => pr.Id).ToList());

        //    var newService = CreateService(packageService: mockPackageService, typosquattingCheckListCacheService: mockTyposquattingCheckListCacheService);

        //    // Act
        //    var typosquattingCheckResult = await newService.IsUploadedPackageIdTyposquattingAsync(GetMockTyposquattingCheckInfo(uploadedPackageId, _uploadedPackageOwner));

        //    // Assert
        //    Assert.True(typosquattingCheckResult.WasUploadBlocked);
        //    Assert.Equal(2, typosquattingCheckResult.TyposquattingCheckCollisionIds.Count());
        //}

        //[Fact]
        //public async Task CheckNotTyposquattingMultiCollisionsWithSameUsersAsync()
        //{
        //    // Arrange            
        //    var uploadedPackageId = "microsoft_netframework.v1";
        //    _uploadedPackageOwner.Username = "owner1";
        //    _uploadedPackageOwner.Key = 1;
        //    var pacakgeRegistrationsList = PackageRegistrationsList.Concat(new PackageRegistration[]
        //    {
        //        new PackageRegistration()
        //        {
        //            Id = "microsoft-netframework-v1",
        //            DownloadCount = new Random().Next(0, 10000),
        //            IsVerified = true,
        //            Owners = new List<User> { new User() { Username = string.Format("owner{0}", _packageIds.Count() + 2), Key = _packageIds.Count() + 2 } }
        //        }
        //    });

        //    var mockPackageService = new Mock<IPackageService>();
        //    mockPackageService
        //        .Setup(x => x.GetAllPackageRegistrations())
        //        .Returns(pacakgeRegistrationsList);
        //    var mockTyposquattingCheckListCacheService = new Mock<ITyposquattingCheckListCacheService>();
        //    mockTyposquattingCheckListCacheService
        //        .Setup(x => x.GetTyposquattingCheckList(It.IsAny<int>(), It.IsAny<TimeSpan>(), It.IsAny<IPackageService>()))
        //        .Returns(pacakgeRegistrationsList.Select(pr => pr.Id).ToList());

        //    var newService = CreateService(packageService: mockPackageService, typosquattingCheckListCacheService: mockTyposquattingCheckListCacheService);

        //    // Act
        //    var typosquattingCheckResult = await newService.IsUploadedPackageIdTyposquattingAsync(GetMockTyposquattingCheckInfo(uploadedPackageId, _uploadedPackageOwner));

        //    // Assert
        //    Assert.False(typosquattingCheckResult.WasUploadBlocked);
        //    Assert.Equal(1, typosquattingCheckResult.TyposquattingCheckCollisionIds.Count());
        //    Assert.Equal("microsoft-netframework-v1", typosquattingCheckResult.TyposquattingCheckCollisionIds.First());
        //}

        //[Fact]
        //public async Task CheckNotTyposquattingWithinReservedNameSpaceAsync()
        //{
        //    // Arrange
        //    var uploadedPackageId = "microsoft_netframework.v1";

        //    var mockReservedNamespaceService = new Mock<IReservedNamespaceService>();
        //    mockReservedNamespaceService
        //        .Setup(x => x.GetReservedNamespacesForId(It.IsAny<string>()))
        //        .Returns(new List<ReservedNamespace> { new ReservedNamespace() });

        //    var newService = CreateService(reservedNamespaceService: mockReservedNamespaceService);

        //    // Act
        //    var typosquattingCheckResult = await newService.IsUploadedPackageIdTyposquattingAsync(GetMockTyposquattingCheckInfo(uploadedPackageId, _uploadedPackageOwner));

        //    // Assert
        //    Assert.False(typosquattingCheckResult.WasUploadBlocked);
        //    Assert.Equal(0, typosquattingCheckResult.TyposquattingCheckCollisionIds.Count());
        //}

        //[Fact]
        //public async Task CheckTyposquattingNullUploadedPackageIdAsync()
        //{
        //    // Arrange
        //    string uploadedPackageId = null;

        //    var newService = CreateService();

        //    // Act
        //    var exception = await Assert.ThrowsAsync<ArgumentNullException>(
        //        async () => await newService.IsUploadedPackageIdTyposquattingAsync(GetMockTyposquattingCheckInfo(uploadedPackageId, _uploadedPackageOwner)));

        //    // Assert
        //    Assert.Equal(nameof(uploadedPackageId), exception.ParamName);
        //}

        //[Fact]
        //public async Task CheckTyposquattingNullUploadedPackageOwnerAsync()
        //{
        //    // Arrange
        //    _uploadedPackageOwner = null;
        //    var uploadedPackageId = "microsoft_netframework_v1";

        //    var newService = CreateService();

        //    // Act
        //    var exception = await Assert.ThrowsAsync<ArgumentNullException>(
        //        async () => await newService.IsUploadedPackageIdTyposquattingAsync(GetMockTyposquattingCheckInfo(uploadedPackageId, _uploadedPackageOwner)));

        //    // Assert
        //    Assert.Equal("uploadedPackageOwner", exception.ParamName);
        //}

        //[Fact]
        //public async Task CheckTyposquattingEmptyUploadedPackageIdAsync()
        //{
        //    // Arrange
        //    var uploadedPackageId = "";

        //    var newService = CreateService();

        //    // Act
        //    var typosquattingCheckResult = await newService.IsUploadedPackageIdTyposquattingAsync(GetMockTyposquattingCheckInfo(uploadedPackageId, _uploadedPackageOwner));

        //    // Assert
        //    Assert.False(typosquattingCheckResult.WasUploadBlocked);
        //    Assert.Equal(0, typosquattingCheckResult.TyposquattingCheckCollisionIds.Count());
        //}

        //[Fact]
        //public async Task CheckTyposquattingEmptyChecklistAsync()
        //{
        //    // Arrange
        //    var uploadedPackageId = "microsoft_netframework_v1";
        //    var mockPackageService = new Mock<IPackageService>();
        //    mockPackageService
        //        .Setup(x => x.GetAllPackageRegistrations())
        //        .Returns(new List<PackageRegistration>().AsQueryable());
        //    var mockTyposquattingCheckListCacheService = new Mock<ITyposquattingCheckListCacheService>();
        //    mockTyposquattingCheckListCacheService
        //        .Setup(x => x.GetTyposquattingCheckList(It.IsAny<int>(), It.IsAny<TimeSpan>(), It.IsAny<IPackageService>()))
        //        .Returns(new List<string>());

        //    var newService = CreateService(packageService: mockPackageService, typosquattingCheckListCacheService: mockTyposquattingCheckListCacheService);

        //    // Act
        //    var typosquattingCheckResult = await newService.IsUploadedPackageIdTyposquattingAsync(GetMockTyposquattingCheckInfo(uploadedPackageId, _uploadedPackageOwner));

        //    // Assert
        //    Assert.False(typosquattingCheckResult.WasUploadBlocked);
        //    Assert.Equal(0, typosquattingCheckResult.TyposquattingCheckCollisionIds.Count());
        //}

        //[Theory]
        //[InlineData("Microsoft_NetFramework_v1")]
        //[InlineData("new_package_for_testing")]
        //public async Task CheckTyposquattingNotEnabledAsync(string packageId)
        //{
        //    // Arrange
        //    var uploadedPackageId = packageId;

        //    var mockFeatureFlagService = new Mock<IFeatureFlagService>();
        //    mockFeatureFlagService
        //        .Setup(f => f.IsTyposquattingEnabled())
        //        .Returns(false);
        //    mockFeatureFlagService
        //        .Setup(f => f.IsTyposquattingEnabled(_uploadedPackageOwner))
        //        .Returns(true);

        //    var newService = CreateService(featureFlagService: mockFeatureFlagService);

        //    // Act
        //    var typosquattingCheckResult = await newService.IsUploadedPackageIdTyposquattingAsync(GetMockTyposquattingCheckInfo(uploadedPackageId, _uploadedPackageOwner));

        //    // Assert
        //    Assert.False(typosquattingCheckResult.WasUploadBlocked);
        //    Assert.Equal(0, typosquattingCheckResult.TyposquattingCheckCollisionIds.Count());

        //    mockFeatureFlagService
        //        .Verify(f => f.IsTyposquattingEnabled(), Times.Once);
        //    mockFeatureFlagService
        //        .Verify(f => f.IsTyposquattingEnabled(It.IsAny<User>()), Times.Never);
        //}

        //[Fact]
        //public async Task CheckNotTyposquattingBlockUserNotEnabledAsync()
        //{
        //    // Arrange
        //    var uploadedPackageId = "new_package_for_testing";

        //    var mockContentObjectService = new Mock<IContentObjectService>();
        //    mockContentObjectService
        //        .Setup(x => x.TyposquattingConfiguration.PackageIdChecklistLength)
        //        .Returns(20000);

        //    var newService = CreateService(contentObjectService: mockContentObjectService);

        //    // Act
        //    var typosquattingCheckResult = await newService.IsUploadedPackageIdTyposquattingAsync(GetMockTyposquattingCheckInfo(uploadedPackageId, _uploadedPackageOwner));

        //    // Assert
        //    Assert.False(typosquattingCheckResult.WasUploadBlocked);
        //    Assert.Equal(0, typosquattingCheckResult.TyposquattingCheckCollisionIds.Count());
        //}

        //[Fact]
        //public async Task CheckIsTyposquattingBlockUserNotEnabledAsync()
        //{
        //    // Arrange
        //    var uploadedPackageId = "Microsoft_NetFramework_v1";

        //    var featureFlagService = new Mock<IFeatureFlagService>();
        //    featureFlagService
        //        .Setup(f => f.IsTyposquattingEnabled())
        //        .Returns(true);
        //    featureFlagService
        //        .Setup(f => f.IsTyposquattingEnabled(_uploadedPackageOwner))
        //        .Returns(false);

        //    var newService = CreateService(featureFlagService: featureFlagService);

        //    // Act
        //    var typosquattingCheckResult = await newService.IsUploadedPackageIdTyposquattingAsync(GetMockTyposquattingCheckInfo(uploadedPackageId, _uploadedPackageOwner));

        //    // Assert
        //    Assert.False(typosquattingCheckResult.WasUploadBlocked);
        //    Assert.Equal(1, typosquattingCheckResult.TyposquattingCheckCollisionIds.Count());
        //    Assert.Equal("microsoft_netframework_v1", typosquattingCheckResult.TyposquattingCheckCollisionIds.First());

        //    featureFlagService
        //        .Verify(f => f.IsTyposquattingEnabled(), Times.Once);
        //    featureFlagService
        //        .Verify(f => f.IsTyposquattingEnabled(_uploadedPackageOwner), Times.Once);
        //}

        //[Fact]
        //public async Task CheckTelemetryServiceLogOriginalUploadedPackageIdAsync()
        //{
        //    // Arrange
        //    var uploadedPackageId = "microsoft_netframework.v1";
        //    var mockTelemetryService = new Mock<ITelemetryService>();

        //    var newService = CreateService(telemetryService: mockTelemetryService);

        //    // Act
        //    var typosquattingCheckResult = await newService.IsUploadedPackageIdTyposquattingAsync(GetMockTyposquattingCheckInfo(uploadedPackageId, _uploadedPackageOwner));

        //    // Assert
        //    mockTelemetryService.Verify(
        //        x => x.TrackMetricForTyposquattingChecklistRetrievalTime(uploadedPackageId, It.IsAny<TimeSpan>()),
        //        Times.Once);

        //    mockTelemetryService.Verify(
        //        x => x.TrackMetricForTyposquattingAlgorithmProcessingTime(uploadedPackageId, It.IsAny<TimeSpan>()),
        //        Times.Once);

        //    mockTelemetryService.Verify(
        //        x => x.TrackMetricForTyposquattingCheckResultAndTotalTime(
        //            uploadedPackageId,
        //            It.IsAny<TimeSpan>(),
        //            It.IsAny<bool>(),
        //            It.IsAny<List<string>>(),
        //            It.IsAny<int>(),
        //            It.IsAny<TimeSpan>()),
        //        Times.Once);

        //    mockTelemetryService.Verify(
        //        x => x.TrackMetricForTyposquattingOwnersCheckTime(uploadedPackageId, It.IsAny<TimeSpan>()),
        //        Times.Once);
        //}

        //[Theory]
        //[InlineData("Microsoft_NetFramework_v1", "Microsoft.NetFramework.v1", 0)]
        //[InlineData("Microsoft_NetFramework_v1", "microsoft-netframework-v1", 0)]
        //[InlineData("Microsoft_NetFramework_v1", "MicrosoftNetFrameworkV1", 0)]
        //[InlineData("Microsoft_NetFramework_v1", "Mícr0s0ft_NetFrάmѐw0rk_v1", 0)]
        //[InlineData("Dotnet.Script.Core.RoslynDependencies", "dotnet-script-core-rõslyndependencies", 1)]
        //[InlineData("Dotnet.Script.Core.RoslynDependencies", "DotnetScriptCoreRoslyndependncies", 1)]
        //[InlineData("MichaelBrandonMorris.Extensions.CollectionExtensions", "Michaelbrandonmorris.Extension.CollectionExtension", 2)]
        //[InlineData("MichaelBrandonMorris.Extensions.CollectionExtensions", "MichaelBrandonMoris_Extensions_CollectionExtension", 2)]
        //public void CheckTyposquattingDistance(string str1, string str2, int threshold)
        //{
        //    // Arrange 
        //    str1 = TyposquattingStringNormalization.NormalizeString(str1);
        //    str2 = TyposquattingStringNormalization.NormalizeString(str2);

        //    // Act
        //    var checkResult = TyposquattingDistanceCalculation.IsDistanceLessThanOrEqualToThreshold(str1, str2, threshold);

        //    // Assert
        //    Assert.True(checkResult);
        //}

        //[Theory]
        //[InlineData("Lappa.ORM", "JCTools.I18N", 0)]
        //[InlineData("Cake.Intellisense.Core", "Cake.IntellisenseGenerator", 0)]
        //[InlineData("Hangfire.Net40", "Hangfire.SqlServer.Net40", 0)]
        //[InlineData("LogoFX.Client.Tests.Integration.SpecFlow.Core", "LogoFX.Client.Testing.EndToEnd.SpecFlow", 1)]
        //[InlineData("cordova-plugin-ms-adal.TypeScript.DefinitelyTyped", "eonasdan-bootstrap-datetimepicker.TypeScript.DefinitelyTyped", 2)]
        //public void CheckNotTyposquattingDistance(string str1, string str2, int threshold)
        //{
        //    // Arrange
        //    str1 = TyposquattingStringNormalization.NormalizeString(str1);
        //    str2 = TyposquattingStringNormalization.NormalizeString(str2);

        //    // Act
        //    var checkResult = TyposquattingDistanceCalculation.IsDistanceLessThanOrEqualToThreshold(str1, str2, threshold);

        //    // Assert
        //    Assert.False(checkResult);
        //}

        //[Theory]
        //[InlineData("ă", "a")]
        //[InlineData("aă", "aa")]
        //[InlineData("aăăa", "aaaa")]
        //[InlineData("𐒎", "h")]
        //[InlineData("h𐒎", "hh")]
        //[InlineData("h𐒎𐒎h", "hhhh")]
        //[InlineData("aă𐒎a", "aaha")]
        //[InlineData("a𐒎ăa", "ahaa")]
        //[InlineData("aă𐒎ăa", "aahaa")]
        //[InlineData("a𐒎ă𐒎a", "ahaha")]
        //[InlineData("aă𐒎ă𐒎a", "aahaha")]
        //[InlineData("aă𐒎𐒎ăă𐒎a", "aahhaaha")]
        //[InlineData("aă𐒎𐒎a𐒎ăă𐒎ă𐒎a", "aahhahaahaha")]
        //[InlineData("Microsoft_NetFramework_v1", "microsoft_netframework_v1")]
        //[InlineData("Microsoft.netframework-v1", "microsoft_netframework_v1")]
        //[InlineData("mícr0s0ft.nёtFrǎmȇwὀrk.v1", "microsoft_netframework_v1")]
        //public void CheckNormalization(string str1, string str2)
        //{
        //    // Arrange and Act
        //    str1 = TyposquattingStringNormalization.NormalizeString(str1);

        //    // Assert
        //    Assert.Equal(str1, str2);
        //}

        //[Fact]
        //public void CheckNormalizationDictionary()
        //{
        //    // Arrange
        //    var similarCharacterDictionary = new Dictionary<string, string>()
        //    {
        //        { "a", "AΑАαаÀÁÂÃÄÅàáâãäåĀāĂăĄąǍǎǞǟǠǡǺǻȀȁȂȃȦȧȺΆάἀἁἂἃἄἅἆἇἈἉἊἋἌΆἍἎἏӐӑӒӓὰάᾀᾁᾂᾃᾄᾅᾆᾇᾈᾊᾋᾌᾍᾎᾏᾰᾱᾲᾳᾴᾶᾷᾸᾹᾺᾼДд"},
        //        { "b", "BΒВЪЬƀƁƂƃƄƅɃḂḃϦЂБвъьѢѣҌҍႦႪხҔҕӃӄ"},
        //        { "c", "CСсϹϲÇçĆćĈĉĊċČčƇƈȻȼҪҫ𐒨"},
        //        { "d", "DƊԁÐĎďĐđƉƋƌǷḊḋԀԂԃ"},
        //        { "e", "EΕЕеÈÉÊËèéêëĒēĔĕĖėĘęĚěȄȅȆȇȨȩɆɇΈЀЁЄѐёҼҽҾҿӖӗἘἙἚἛἜἝῈΈ"},
        //        { "f", "FϜƑƒḞḟϝҒғӺӻ"},
        //        { "g", "GǤԌĜĝĞğĠġĢģƓǥǦǧǴǵԍ"},
        //        { "h", "HΗНһհҺĤĥħǶȞȟΉἨἩἪἫἬἭἮἯᾘᾙᾚᾛᾜᾝᾞᾟῊΉῌЋнћҢңҤҥӇӈӉӊԊԋԦԧԨԩႬႹ𐒅𐒌𐒎𐒣"},
        //        { "i", "IΙІӀ¡ìíîïǐȉȋΐίιϊіїὶίῐῑῒΐῖῗΊΪȊȈἰἱἲἳἴἵἶἷἸἹἺἻἼἽἾἿῘῙῚΊЇӏÌÍÎÏĨĩĪīĬĭĮįİǏ"},
        //        { "j", "JЈͿϳĴĵǰȷ"},
        //        { "k", "KΚКKĶķĸƘƙǨǩκϏЌкќҚқҜҝҞҟҠҡԞԟ"},
        //        { "l", "LĹĺĻļĽľĿŀŁłſƖƪȴẛ"},
        //        { "m", "MΜМṀṁϺϻмӍӎ𐒄"},
        //        { "n", "NΝпÑñŃńŅņŇňŉƝǸǹᾐᾑᾒᾓᾔᾕᾖᾗῂῃῄῆῇԤԥԮԯ𐒐"},
        //        { "o", "OΟОՕჿоοÒÓÔÕÖðòóôõöøŌōŎŏŐőƠơǑǒǪǫǬǭȌȍȎȏȪȫȬȭȮȯȰȱΌδόϘϙὀὁὂὃὄὅὈὉὊὋὌὍὸόῸΌӦӧჾ𐒆𐒠0"},
        //        { "p", "PΡРрρÞþƤƥƿṖṗϷϸῤῥῬҎҏႲႼ"},
        //        { "q", "QգԛȡɊɋԚႭႳ"},
        //        { "r", "RгŔŕŖŗŘřƦȐȑȒȓɌɼѓ"},
        //        { "s", "SЅѕՏႽჽŚśŜŝŞşŠšȘșȿṠṡ𐒖𐒡"},
        //        { "t", "TΤТͲͳŢţŤťŦŧƬƭƮȚțȾṪṫτтҬҭէ"},
        //        { "u", "UՍႮÙÚÛÜùúûüŨũŪūŬŭŮůŰűŲųƯưǓǔǕǖǗǘǙǚǛǜȔȕȖȗμυϋύὐὑὒὓὔὕὖὗὺύῠῡῢΰῦῧ𐒩"},
        //        { "v", "VνѴѵƔƲѶѷ"},
        //        { "w", "WωшԜԝŴŵƜẀẁẂẃẄẅώШЩщѡѿὠὡὢὣὤὥὦὧὼώᾠᾡᾢᾣᾤᾥᾦᾧῲῳῴῶῷ"},
        //        { "x", "XХΧх×χҲҳӼӽӾӿჯ"},
        //        { "y", "YΥҮƳуУÝýÿŶŷŸƴȲȳɎɏỲỳΎΫγϒϓϔЎЧўүҶҷҸҹӋӌӮӯӰӱӲӳӴӵὙὛὝὟῨῩῪΎႯႸ𐒋𐒦"},
        //        { "z", "ZΖჍŹźŻżŽžƵƶȤȥ"},
        //        { "3", "ƷЗʒӡჳǮǯȜȝзэӞӟӠ"},
        //        { "8", "Ȣȣ"},
        //        { "_", ".-" }
        //    };

        //    var testPackageName = "testpackage";
        //    foreach (var item in similarCharacterDictionary)
        //    {
        //        var textElementEnumerator = StringInfo.GetTextElementEnumerator(item.Value);
        //        while (textElementEnumerator.MoveNext())
        //        {
        //            var typoString = testPackageName + textElementEnumerator.GetTextElement();
        //            var baseString = testPackageName + item.Key;

        //            // Act
        //            var normalizedString = TyposquattingStringNormalization.NormalizeString(typoString);

        //            // Assert
        //            Assert.Equal(baseString, normalizedString);
        //        }
        //    }
        //}

        //private TyposquattingCheckInfo GetMockTyposquattingCheckInfo(string uploadedPackageId, User uploadedPackageOwner) => 
        //    new TyposquattingCheckInfo(
        //        uploadedPackageId: uploadedPackageId,
        //        uploadedPackageOwner: uploadedPackageOwner,
        //        allPackageRegistrations: It.IsAny<IQueryable<PackageRegistration>>(),
        //        checkListConfiguredLength: It.IsAny<int>(),
        //        checkListExpireTimeInHours: It.IsAny<TimeSpan>(),
        //        isTyposquattingEnabledForOwner: It.IsAny<bool>());
    }
}