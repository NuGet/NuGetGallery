using System;
using System.IO;
using Moq;
using NuGet;
using Xunit;
using Xunit.Extensions;

namespace NuGetGallery
{
    public class PackageUploadFileServiceFacts
    {
        public class TheSaveUploadedFileMethod
        {
            [Fact]
            public void WillThrowIfTheUserKeyIsMissing()
            {
                var service = CreateService();

                var ex = Assert.Throws<ArgumentException>(() =>
                {
                    service.SaveUploadedFile(0, "thePackageId", "thePackageVersion", new MemoryStream());
                });

                Assert.Equal("userKey", ex.ParamName);
            }

            [Theory]
            [InlineData(new object[]{ (string)null })]
            [InlineData(new object[] { "" })]
            [InlineData(new object[] { " " })]
            public void WillThrowIfThePackageIdIsNullOrEmptyOrWhitespace(string packageId)
            {
                var fakePackage = new Mock<IPackageMetadata>();
                fakePackage.Setup(x => x.Id).Returns(packageId);
                var service = CreateService();

                var ex = Assert.Throws<ArgumentException>(() =>
                {
                    service.SaveUploadedFile(1, packageId, "thePackageVersion", new MemoryStream());
                });

                Assert.Equal("packageId", ex.ParamName);
            }

            [Theory]
            [InlineData(new object[] { (string)null })]
            [InlineData(new object[] { "" })]
            [InlineData(new object[] { " " })]
            public void WillThrowIfThePackageVersionIsNull(string packageVersion)
            {
                var fakePackage = new Mock<IPackageMetadata>();
                fakePackage.Setup(x => x.Id).Returns("theId");
                var service = CreateService();

                var ex = Assert.Throws<ArgumentException>(() =>
                {
                    service.SaveUploadedFile(1, "thePackageId", packageVersion, new MemoryStream());
                });

                Assert.Equal("packageVersion", ex.ParamName);
            }
        }

        static PackageUploadFileService CreateService()
        {
            return new PackageUploadFileService();
        }
    }
}
