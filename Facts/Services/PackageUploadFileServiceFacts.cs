using System;
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
            public void WillThrowIfTheUserIsNull()
            {
                var fakePackage = new Mock<IPackageMetadata>();
                var service = CreateService();

                var ex = Assert.Throws<ArgumentNullException>(() =>
                {
                    service.SaveUploadedFile(null, fakePackage.Object);
                });

                Assert.Equal("user", ex.ParamName);
            }

            [Fact]
            public void WillThrowIfThePackageIsNull()
            {
                var service = CreateService();

                var ex = Assert.Throws<ArgumentNullException>(() =>
                {
                    service.SaveUploadedFile(new User(), null);
                });

                Assert.Equal("package", ex.ParamName);
            }

            [Theory]
            [InlineData(new object[]{ (string)null })]
            [InlineData(new object[] { "" })]
            [InlineData(new object[] { " " })]
            public void WillThrowIfThePackageIdIsNullOrEmptyOrWhitespace(string id)
            {
                var fakePackage = new Mock<IPackageMetadata>();
                fakePackage.Setup(x => x.Id).Returns(id);
                var service = CreateService();

                var ex = Assert.Throws<ArgumentException>(() =>
                {
                    service.SaveUploadedFile(new User(), fakePackage.Object);
                });

                Assert.Equal("package", ex.ParamName);
            }

            [Fact]
            public void WillThrowIfThePackageVersionIsNull()
            {
                var fakePackage = new Mock<IPackageMetadata>();
                fakePackage.Setup(x => x.Id).Returns("theId");
                var service = CreateService();

                var ex = Assert.Throws<ArgumentException>(() =>
                {
                    service.SaveUploadedFile(new User(), fakePackage.Object);
                });

                Assert.Equal("package", ex.ParamName);
            }
        }

        static PackageUploadFileService CreateService()
        {
            return new PackageUploadFileService();
        }
    }
}
