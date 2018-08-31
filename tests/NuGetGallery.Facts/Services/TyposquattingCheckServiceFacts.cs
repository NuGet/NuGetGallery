// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using Moq;
using Xunit;

namespace NuGetGallery
{
    public class TyposquattingCheckServiceFacts
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

        private static List<PackageRegistration> _pacakgeRegistrationsList = Enumerable.Range(0, _packageIds.Count()).Select(i =>
                new PackageRegistration()
                {
                    Id = _packageIds[i],
                    DownloadCount = new Random().Next(0, 10000),
                    IsVerified = true,
                }).ToList();

        private static Mock<ITyposquattingUserService> _typosquattingUserService = new Mock<ITyposquattingUserService>();
        private static Mock<IEntityRepository<PackageRegistration>> _packageRegistrationRepository = new Mock<IEntityRepository<PackageRegistration>>();

        [Fact]
        public void CheckNotTyposquattingByDifferentOwnersTest()
        {
            // Arrange            
            var uploadedPackageOwner = new User();
            var uploadedPackageId = "new_package_for_testing";

            _typosquattingUserService
                .Setup(x => x.CanUserTyposquat(It.IsAny<string>(), It.IsAny<string>()))
                .Returns(false);
            _packageRegistrationRepository
                .Setup(x => x.GetAll())
                .Returns(_pacakgeRegistrationsList.AsQueryable());

            var newService = new TyposquattingCheckService(_typosquattingUserService.Object, _packageRegistrationRepository.Object);

            // Act
            var typosquattingCheckResult = newService.IsUploadedPackageIdTyposquatting(uploadedPackageId, uploadedPackageOwner);

            // Assert
            Assert.False(typosquattingCheckResult);
        }

        [Fact]
        public void CheckNotTyposquattingBySameOwnersTest()
        {
            // Arrange            
            var uploadedPackageOwner = new User();
            uploadedPackageOwner.Username = "owner1";
            var uploadedPackageId = "microsoft_netframework.v1";

            _typosquattingUserService
                .Setup(x => x.CanUserTyposquat(It.IsAny<string>(), It.IsAny<string>()))
                .Returns(true);
            _packageRegistrationRepository
                .Setup(x => x.GetAll())
                .Returns(_pacakgeRegistrationsList.AsQueryable());

            var newService = new TyposquattingCheckService(_typosquattingUserService.Object, _packageRegistrationRepository.Object);
            
            // Act
            var typosquattingCheckResult = newService.IsUploadedPackageIdTyposquatting(uploadedPackageId, uploadedPackageOwner);

            // Assert
            Assert.False(typosquattingCheckResult);
        }

        [Fact]
        public void CheckTyposquattingByDifferentOwnersTest()
        {
            // Arrange            
            var uploadedPackageOwner = new User();
            var uploadedPackageId = "Mícrosoft.NetFramew0rk.v1";

            _typosquattingUserService
                .Setup(x => x.CanUserTyposquat(It.IsAny<string>(), It.IsAny<string>()))
                .Returns(false);
            _packageRegistrationRepository
                .Setup(x => x.GetAll())
                .Returns(_pacakgeRegistrationsList.AsQueryable());

            var newService = new TyposquattingCheckService(_typosquattingUserService.Object, _packageRegistrationRepository.Object);

            // Act
            var typosquattingCheckResult = newService.IsUploadedPackageIdTyposquatting(uploadedPackageId, uploadedPackageOwner);

            // Assert
            Assert.True(typosquattingCheckResult);
        }

        [Fact]
        public void CheckTyposquattingNullUploadedPackageId()
        {
            // Arrange            
            var uploadedPackageOwner = new User();
            string uploadedPackageId = null;

            _packageRegistrationRepository
                .Setup(x => x.GetAll())
                .Returns(_pacakgeRegistrationsList.AsQueryable());

            var newService = new TyposquattingCheckService(_typosquattingUserService.Object, _packageRegistrationRepository.Object);
            
            // Act
            var exception = Assert.Throws<ArgumentNullException>(
                () => newService.IsUploadedPackageIdTyposquatting(uploadedPackageId, uploadedPackageOwner));

            // Assert
            Assert.Equal(nameof(uploadedPackageId), exception.ParamName);
        }

        [Fact]
        public void CheckTyposquattingNullUploadedPackageOwner()
        {
            // Arrange
            User uploadedPackageOwner = null;
            var uploadedPackageId = "microsoft_netframework_v1";

            _packageRegistrationRepository
                .Setup(x => x.GetAll())
                .Returns(_pacakgeRegistrationsList.AsQueryable());

            var newService = new TyposquattingCheckService(_typosquattingUserService.Object, _packageRegistrationRepository.Object);
  
            // Act
            var exception = Assert.Throws<ArgumentNullException>(
                () => newService.IsUploadedPackageIdTyposquatting(uploadedPackageId, uploadedPackageOwner));

            // Assert
            Assert.Equal(nameof(uploadedPackageOwner), exception.ParamName);
        }

        [Fact]
        public void CheckTyposquattingEmptyChecklist()
        {
            // Arrange            
            var uploadedPackageOwner = new User();
            var uploadedPackageId = "microsoft_netframework_v1";

            _typosquattingUserService
                .Setup(x => x.CanUserTyposquat(It.IsAny<string>(), It.IsAny<string>()))
                .Returns(false);
            _packageRegistrationRepository
                .Setup(x => x.GetAll())
                .Returns(new List<PackageRegistration>().AsQueryable());

            var newService = new TyposquattingCheckService(_typosquattingUserService.Object, _packageRegistrationRepository.Object);

            // Act
            var typosquattingCheckResult = newService.IsUploadedPackageIdTyposquatting(uploadedPackageId, uploadedPackageOwner);

            // Assert
            Assert.False(typosquattingCheckResult);
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