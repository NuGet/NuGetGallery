using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Moq;
using Xunit;

namespace NuGetGallery
{
    public class TyposquattingCheckServiceFacts
    {
        private static List<ThresholdInfo> _thresholdsList = new List<ThresholdInfo>
        {
            new ThresholdInfo { LowerBound = 0, UpperBound = 30, Threshold = 0 },
            new ThresholdInfo { LowerBound = 30, UpperBound = 50, Threshold = 1 },
            new ThresholdInfo { LowerBound = 50, UpperBound = 120, Threshold = 2 }
        };

        private static List<PackageInfo> _checkList = new List<PackageInfo>
        {
            new PackageInfo { Id = "microsoft_netframework_v1", Owners = new HashSet<string> { "owner1" } },
            new PackageInfo { Id = "resxtocs_core", Owners = new HashSet<string> { "owner2" } },
            new PackageInfo { Id = "gisrestapi", Owners = new HashSet<string> { "owner3" } },
            new PackageInfo { Id = "xamarinfirebase", Owners = new HashSet<string> { "owner4" } },
            new PackageInfo { Id = "shsoft_infrastructure", Owners = new HashSet<string> { "owner5" } },
            new PackageInfo { Id = "telegram_net_core", Owners = new HashSet<string> { "owner6" } },
            new PackageInfo { Id = "selenium_webDriver_microsoftdriver", Owners = new HashSet<string> { "owner7" } }
        };

        private static Mock<IPackageService> _packageService = new Mock<IPackageService>();

        [Fact]
        public void CheckNotTyposquattingByDifferentOwnersTest()
        {
            // Arrange            
            User owner = new User();
            string uploadedPackageId = "new_package_for_testing";
            TyposquattingCheckService newService = new TyposquattingCheckService(_checkList, _thresholdsList, _packageService.Object);

            // Act
            var typosquattingCheckResult = newService.IsUploadedPackageIdTyposquatting(uploadedPackageId, owner);

            // Assert
            Assert.False(typosquattingCheckResult);
        }

        [Fact]
        public void CheckNotTyposquattingBySameOwnersTest()
        {
            // Arrange            
            User owner = new User();
            owner.Username = "owner1";
            string uploadedPackageId = "microsoft_netframework.v1";
            TyposquattingCheckService newService = new TyposquattingCheckService(_checkList, _thresholdsList, _packageService.Object);

            // Act
            var typosquattingCheckResult = newService.IsUploadedPackageIdTyposquatting(uploadedPackageId, owner);

            // Assert
            Assert.False(typosquattingCheckResult);
        }

        [Fact]
        public void CheckNotTyposquattingByDifferentOwnersThroughDoubleCheckTest()
        {
            // Arrange            
            User owner = new User();
            owner.Username = "newOwner1";
            string uploadedPackageId = "Microsoft_NetFramework.v1";

            PackageRegistration package = new PackageRegistration();
            package.Id = "microsoft_netframework_v1";
            package.Owners.Add(owner);

            _packageService
                .Setup(x => x.FindPackageRegistrationById(It.IsAny<string>()))
                .Returns(() => package);
            TyposquattingCheckService newService = new TyposquattingCheckService(_checkList, _thresholdsList, _packageService.Object);

            // Act
            var typosquattingCheckResult = newService.IsUploadedPackageIdTyposquatting(uploadedPackageId, owner);

            // Assert
            Assert.False(typosquattingCheckResult);
        }

        [Fact]
        public void CheckTyposquattingByDifferentOwnersTest()
        {
            // Arrange            
            User owner = new User();
            string uploadedPackageId = "Mícrosoft.NetFramew0rk.v1";
            TyposquattingCheckService newService = new TyposquattingCheckService(_checkList, _thresholdsList, _packageService.Object);

            // Act
            var typosquattingCheckResult = newService.IsUploadedPackageIdTyposquatting(uploadedPackageId, owner);

            // Assert
            Assert.True(typosquattingCheckResult);
        }

        [Fact]
        public void CheckTyposquattingNulluploadedPackageId()
        {
            // Arrange            
            User owner = new User();
            string uploadedPackageId = null;
            TyposquattingCheckService newService = new TyposquattingCheckService(_checkList, _thresholdsList, _packageService.Object);

            // Act
            var exception = Assert.Throws<ArgumentNullException>(
                () => newService.IsUploadedPackageIdTyposquatting(uploadedPackageId, owner));

            // Assert
            Assert.Equal(nameof(uploadedPackageId), exception.ParamName);
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
        public void CheckTyposquattingDistance(string str1, string str2, int  threshold)
        {
            // Arrange 
            str1 = TyposquattingCheckService.NormalizeString(str1);
            str2 = TyposquattingCheckService.NormalizeString(str2);

            // Act
            var checkResult = TyposquattingCheckService.IsDistanceLessThanThreshold(str1, str2, threshold);

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
            str1 = TyposquattingCheckService.NormalizeString(str1);
            str2 = TyposquattingCheckService.NormalizeString(str2);

            // Act
            var checkResult = TyposquattingCheckService.IsDistanceLessThanThreshold(str1, str2, threshold);

            // Assert
            Assert.False(checkResult);
        }

        [Fact]
        public void CheckDistanceNullInput()
        {
            // Arrange            
            string str1 = null;
            string str2 = "Lappa.ORM";
            int threshold = 0;

            // Act
            var exception = Assert.Throws<ArgumentNullException>(
                () => TyposquattingCheckService.IsDistanceLessThanThreshold(str1, str2, threshold));

            // Assert
            Assert.Equal(nameof(str1), exception.ParamName);
        }


        [Theory]
        [InlineData("Microsoft_NetFramework_v1", "microsoft_netframework_v1")]
        [InlineData("Microsoft.NetFramework-v1", "microsoft_netframework_v1")]
        [InlineData("mícr0s0ft.nёtFrǎmȇwὀrk.v1", "microsoft_netframework_v1")]
        public void CheckNormalization(string str1, string str2)
        {
            // Arrange and Act
            str1 = TyposquattingCheckService.NormalizeString(str1);
          
            // Assert
            Assert.Equal(str1, str2);
        }
    }
}
