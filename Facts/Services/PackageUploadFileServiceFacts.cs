using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Xunit;
using Moq;
using NuGet;

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
        }

        static PackageUploadFileService CreateService()
        {
            return new PackageUploadFileService();
        }
    }
}
