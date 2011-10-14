using System;
using System.IO;
using System.Web.Mvc;
using Microsoft.WindowsAzure.StorageClient;
using Moq;
using Xunit;

namespace NuGetGallery
{
    public class CloudBlobPackageFileServiceFacts
    {
        public class TheCtor
        {
            [Fact]
            public void WillGetThePackagesBlobContainer()
            {
                var blobClient = new Mock<ICloudBlobClient>();

                var service = CreateService(fakeBlobClient: blobClient);

                blobClient.Verify(x => x.GetContainerReference("packages"));
            }

            [Fact]
            public void WillCreateThePackagesBlobContainerIfItDoesNotExist()
            {
                var blobContainer = new Mock<ICloudBlobContainer>();

                var service = CreateService(fakeBlobContainer: blobContainer);

                blobContainer.Verify(x => x.CreateIfNotExist());
            }

            [Fact]
            public void WillSetThePackagesBlobContainerPermissionsToPublic()
            {
                var blobContainer = new Mock<ICloudBlobContainer>();

                var service = CreateService(fakeBlobContainer: blobContainer);

                blobContainer.Verify(x => x.SetPermissions(It.Is<BlobContainerPermissions>(bcp => bcp.PublicAccess == BlobContainerPublicAccessType.Blob)));
            }
        }

        public class TheCreateDownloadPackageResultMethod
        {
            [Fact]
            public void WillGetTheBlobReferenceByUri()
            {
                var fakeBlobContainer = new Mock<ICloudBlobContainer>();
                var service = CreateService(fakeBlobContainer: fakeBlobContainer);

                service.CreateDownloadPackageResult(new Package
                {
                    PackageRegistration = new PackageRegistration
                    {
                        Id = "theId"
                    },
                    Version = "theVersion"
                });

                fakeBlobContainer.Verify(x => x.GetBlobReference(string.Format(Const.PackageFileSavePathTemplate, "theId", "theVersion", Const.PackageFileExtension)));
            }

            [Fact]
            public void WillReturnARedirectResultToTheBlobUri()
            {
                var fakeBlob = new Mock<ICloudBlob>();
                fakeBlob.Setup(x => x.Uri).Returns(new Uri("http://theUri"));
                var service = CreateService(fakeBlob: fakeBlob);

                var result = service.CreateDownloadPackageResult(new Package
                {
                    PackageRegistration = new PackageRegistration
                    {
                        Id = "theId"
                    },
                    Version = "theVersion"
                }) as RedirectResult;

                Assert.NotNull(result);
                Assert.Equal("http://theuri/", result.Url);
            }
        }

        public class TheDeletePackageFileMethod
        {
            [Fact]
            public void WillGetTheBlobReferenceByUri()
            {
                var fakeBlobContainer = new Mock<ICloudBlobContainer>();
                var service = CreateService(fakeBlobContainer: fakeBlobContainer);

                service.DeletePackageFile("theId", "theVersion");

                fakeBlobContainer.Verify(x => x.GetBlobReference(string.Format(Const.PackageFileSavePathTemplate, "theId", "theVersion", Const.PackageFileExtension)));
            }

            [Fact]
            public void WillDeleteTheBlobIfItExists()
            {
                var fakeBlob = new Mock<ICloudBlob>();
                var service = CreateService(fakeBlob: fakeBlob);

                service.DeletePackageFile("theId", "theVersion");

                fakeBlob.Verify(x => x.DeleteIfExists());
            }
        }

        public class TheSavePackageFileMethod
        {
            [Fact]
            public void WillGetTheBlobReferenceByUri()
            {
                var fakeBlobContainer = new Mock<ICloudBlobContainer>();
                var service = CreateService(fakeBlobContainer: fakeBlobContainer);

                service.SavePackageFile(new Package
                {
                    PackageRegistration = new PackageRegistration
                    {
                        Id = "theId"
                    },
                    Version = "theVersion"
                }, new MemoryStream());

                fakeBlobContainer.Verify(x => x.GetBlobReference(string.Format(Const.PackageFileSavePathTemplate, "theId", "theVersion", Const.PackageFileExtension)));

                fakeBlobContainer.Verify(x => x.GetBlobReference(string.Format(Const.PackageFileSavePathTemplate, "theId", "theVersion", Const.PackageFileExtension)));
            }

            [Fact]
            public void WillDeleteTheBlobIfItExists()
            {
                var fakeBlob = new Mock<ICloudBlob>();
                var service = CreateService(fakeBlob: fakeBlob);

                service.SavePackageFile(new Package
                {
                    PackageRegistration = new PackageRegistration
                    {
                        Id = "theId"
                    },
                    Version = "theVersion"
                }, new MemoryStream());

                fakeBlob.Verify(x => x.DeleteIfExists());
            }

            [Fact]
            public void WillUploadThePackageFileToTheBlob()
            {
                var fakeBlob = new Mock<ICloudBlob>();
                var service = CreateService(fakeBlob: fakeBlob);
                var fakePackageFile = new MemoryStream();

                service.SavePackageFile(new Package
                {
                    PackageRegistration = new PackageRegistration
                    {
                        Id = "theId"
                    },
                    Version = "theVersion"
                }, fakePackageFile);

                fakeBlob.Verify(x => x.UploadFromStream(fakePackageFile));
            }
        }

        static CloudBlobPackageFileService CreateService(
            Mock<ICloudBlobClient> fakeBlobClient = null,
            Mock<ICloudBlobContainer> fakeBlobContainer = null,
            Mock<ICloudBlob> fakeBlob = null)
        {
            if (fakeBlobClient == null)
                fakeBlobClient = new Mock<ICloudBlobClient>();
            if (fakeBlobContainer == null)
                fakeBlobContainer = new Mock<ICloudBlobContainer>();
            if (fakeBlob == null)
            {
                fakeBlob = new Mock<ICloudBlob>();
                fakeBlob.Setup(x => x.Uri).Returns(new Uri("http://aUri"));
            }

            fakeBlobClient.Setup(x => x.GetContainerReference(It.IsAny<string>())).Returns(fakeBlobContainer.Object);
            fakeBlobContainer.Setup(x => x.GetBlobReference(It.IsAny<string>())).Returns(fakeBlob.Object);

            return new CloudBlobPackageFileService(fakeBlobClient.Object);
        }
    }
}
