// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using Moq;
using NuGetGallery.Diagnostics;
using Xunit;

namespace NuGetGallery
{
    public class CloudBlobCoreFileStorageServiceFacts
    {
        private static CloudBlobCoreFileStorageService CreateService(
            Mock<ICloudBlobClient> fakeBlobClient = null, 
            Mock<ICloudBlobContainerInformationProvider> fakeFolderInformationProvider = null)
        {
            if (fakeBlobClient == null)
            {
                fakeBlobClient = new Mock<ICloudBlobClient>();
            }

            if (fakeFolderInformationProvider == null)
            {
                fakeFolderInformationProvider = new Mock<ICloudBlobContainerInformationProvider>();
                fakeFolderInformationProvider
                    .Setup(fip => fip.IsPublicContainer(It.IsAny<string>()))
                    .Returns(false);
                fakeFolderInformationProvider
                    .Setup(fip => fip.GetContentType(It.IsAny<string>()))
                    .Returns("application/octet-stream");
                fakeFolderInformationProvider
                    .Setup(fip => fip.GetCacheControl(It.IsAny<string>()))
                    .Returns<string>(null);
            }

            return new CloudBlobCoreFileStorageService(fakeBlobClient.Object, Mock.Of<IDiagnosticsService>(), fakeFolderInformationProvider.Object);
        }

        public class TheCtor
        {
            [Theory]
            [FolderNamesData]
            public async Task WillCreateABlobContainerForDemandedFoldersIfTheyDoNotExist(string folderName)
            {
                var fakeBlobClient = new Mock<ICloudBlobClient>();
                var fakeBlobContainer = new Mock<ICloudBlobContainer>();
                fakeBlobContainer.Setup(x => x.CreateIfNotExistAsync(It.IsAny<bool>())).Returns(Task.FromResult(0)).Verifiable();
                var simpleCloudBlob = new Mock<ISimpleCloudBlob>();
                simpleCloudBlob.Setup(x => x.DownloadToStreamAsync(It.IsAny<Stream>(), It.IsAny<IAccessCondition>())).Returns(Task.FromResult(0));
                fakeBlobContainer.Setup(x => x.GetBlobReference("x.txt")).Returns(simpleCloudBlob.Object);

                fakeBlobClient.Setup(x => x.GetContainerReference(It.IsAny<string>())).Returns(fakeBlobContainer.Object);

                var service = CreateService(fakeBlobClient);
                await service.GetFileAsync(folderName, "x.txt");

                fakeBlobClient.Verify(x => x.GetContainerReference(folderName));
                fakeBlobContainer.Verify();
            }

            [Theory]
            [FolderNamesData(includePermissions: true)]
            public async Task WillSetPermissionsForDemandedFolderInBlobContainers(string folderName, bool isPublic)
            {
                var fakeBlobContainer = new Mock<ICloudBlobContainer>();
                
                var simpleCloudBlob = new Mock<ISimpleCloudBlob>();
                simpleCloudBlob.Setup(x => x.DownloadToStreamAsync(It.IsAny<Stream>(), It.IsAny<IAccessCondition>())).Returns(Task.FromResult(0));

                fakeBlobContainer.Setup(x => x.CreateIfNotExistAsync(It.IsAny<bool>())).Returns(Task.FromResult(0)).Verifiable();
                fakeBlobContainer.Setup(x => x.GetBlobReference("x.txt")).Returns(simpleCloudBlob.Object);

                var fakeBlobClient = new Mock<ICloudBlobClient>();
                fakeBlobClient.Setup(x => x.GetContainerReference(It.IsAny<string>())).Returns(fakeBlobContainer.Object);

                var service = CreateService(fakeBlobClient);
                await service.GetFileAsync(folderName, "x.txt");

                fakeBlobClient.Verify(x => x.GetContainerReference(folderName));
                fakeBlobContainer.Verify();
            }
        }

        public class TheDeletePackageFileMethod
        {
            [Theory]
            [FolderNamesData]
            public async Task WillGetTheBlobFromTheCorrectFolderContainer(string folderName)
            {
                var fakeBlobClient = new Mock<ICloudBlobClient>();
                var fakeBlobContainer = new Mock<ICloudBlobContainer>();
                var fakeBlob = new Mock<ISimpleCloudBlob>();
                fakeBlobClient.Setup(x => x.GetContainerReference(It.IsAny<string>()))
                    .Returns<string>(
                        container =>
                            {
                                Mock<ICloudBlobContainer> blobContainer;
                                if (container == folderName)
                                {
                                    blobContainer = fakeBlobContainer;
                                }
                                else
                                {
                                    blobContainer = new Mock<ICloudBlobContainer>();
                                }
                                blobContainer.Setup(x => x.CreateIfNotExistAsync(It.IsAny<bool>())).Returns(Task.FromResult(0));
                                return blobContainer.Object;
                            });
                fakeBlobContainer.Setup(x => x.GetBlobReference(It.IsAny<string>())).Returns(fakeBlob.Object);
                fakeBlob.Setup(x => x.Uri).Returns(new Uri("http://theUri"));
                fakeBlob.Setup(x => x.DeleteIfExistsAsync()).Returns(Task.FromResult(0));
                fakeBlob.Setup(x => x.SetPropertiesAsync()).Returns(Task.FromResult(0));
                var service = CreateService(fakeBlobClient: fakeBlobClient);

                await service.DeleteFileAsync(folderName, "theFileName");

                fakeBlobContainer.Verify(x => x.GetBlobReference("theFileName"));
            }

            [Fact]
            public async Task WillDeleteTheBlobIfItExists()
            {
                var fakeBlobClient = new Mock<ICloudBlobClient>();
                var fakeBlobContainer = new Mock<ICloudBlobContainer>();
                var fakeBlob = new Mock<ISimpleCloudBlob>();
                fakeBlob.Setup(x => x.DeleteIfExistsAsync()).Returns(Task.FromResult(0)).Verifiable();
                fakeBlobClient.Setup(x => x.GetContainerReference(It.IsAny<string>())).Returns(fakeBlobContainer.Object);
                fakeBlobContainer.Setup(x => x.GetBlobReference(It.IsAny<string>())).Returns(fakeBlob.Object);
                fakeBlobContainer.Setup(x => x.CreateIfNotExistAsync(It.IsAny<bool>())).Returns(Task.FromResult(0));
                fakeBlob.Setup(x => x.Uri).Returns(new Uri("http://theUri"));
                var service = CreateService(fakeBlobClient: fakeBlobClient);

                await service.DeleteFileAsync(CoreConstants.Folders.PackagesFolderName, "theFileName");

                fakeBlob.Verify();
            }
        }

        public class TheGetFileMethod
        {
            [Theory]
            [InlineData(null)]
            [InlineData("")]
            [InlineData(" ")]
            public async Task WillThrowIfFileNameIsNull(string fileName)
            {
                var fakeBlobClient = new Mock<ICloudBlobClient>();
                var fakeBlobContainer = new Mock<ICloudBlobContainer>();
                fakeBlobClient.Setup(x => x.GetContainerReference(It.IsAny<string>())).Returns(fakeBlobContainer.Object);
                var service = CreateService(fakeBlobClient: fakeBlobClient);

                var ex = await Assert.ThrowsAsync<ArgumentNullException>(() => service.GetFileAsync("theFolderName", fileName));
                Assert.Equal("fileName", ex.ParamName);
            }

            [Theory]
            [InlineData(null)]
            [InlineData("")]
            [InlineData(" ")]
            public async Task WillThrowIfFolderNameIsNull(string folderName)
            {
                var fakeBlobClient = new Mock<ICloudBlobClient>();
                var fakeBlobContainer = new Mock<ICloudBlobContainer>();
                fakeBlobClient.Setup(x => x.GetContainerReference(It.IsAny<string>())).Returns(fakeBlobContainer.Object);
                var service = CreateService(fakeBlobClient: fakeBlobClient);

                var ex = await Assert.ThrowsAsync<ArgumentNullException>(() => service.GetFileAsync(folderName, "theFileName"));

                Assert.Equal("folderName", ex.ParamName);
            }

            [Theory]
            [FolderNamesData]
            public async Task WillDownloadTheFile(string folderName)
            {
                var fakeBlobClient = new Mock<ICloudBlobClient>();
                var fakeBlobContainer = new Mock<ICloudBlobContainer>();

                var fakeBlob = new Mock<ISimpleCloudBlob>();

                fakeBlobClient.Setup(x => x.GetContainerReference(It.IsAny<string>()))
                    .Returns<string>(
                        container =>
                            {
                                Mock<ICloudBlobContainer> containerMock;
                                if (container == folderName)
                                {
                                    containerMock = fakeBlobContainer;
                                }
                                else
                                {
                                    containerMock = new Mock<ICloudBlobContainer>();
                                }

                                containerMock.Setup(x => x.CreateIfNotExistAsync(It.IsAny<bool>())).Returns(Task.FromResult(0));
                                return containerMock.Object;
                            });
                fakeBlobContainer.Setup(x => x.GetBlobReference(It.IsAny<string>())).Returns(fakeBlob.Object);
                fakeBlob.Setup(x => x.DownloadToStreamAsync(It.IsAny<Stream>(), It.IsAny<IAccessCondition>())).Returns(Task.FromResult(0)).Verifiable();
                var service = CreateService(fakeBlobClient: fakeBlobClient);

                await service.GetFileAsync(folderName, "theFileName");

                fakeBlob.Verify();
            }

            [Theory]
            [FolderNamesData]
            public async Task WillReturnTheStreamWhenTheFileExists(string folderName)
            {
                var fakeBlobClient = new Mock<ICloudBlobClient>();
                var fakeBlobContainer = new Mock<ICloudBlobContainer>();
                var fakeBlob = new Mock<ISimpleCloudBlob>();
                fakeBlob.Setup(x => x.DeleteIfExistsAsync()).Returns(Task.FromResult(0));
                fakeBlob.Setup(x => x.SetPropertiesAsync()).Returns(Task.FromResult(0));
                fakeBlobClient.Setup(x => x.GetContainerReference(It.IsAny<string>()))
                    .Returns<string>(
                        container =>
                            {
                                Mock<ICloudBlobContainer> blobContainer;
                                if (container == folderName)
                                {
                                    blobContainer = fakeBlobContainer;
                                }
                                else
                                {
                                    blobContainer = new Mock<ICloudBlobContainer>();
                                }
                                blobContainer.Setup(x => x.CreateIfNotExistAsync(It.IsAny<bool>())).Returns(Task.FromResult(0));
                                return blobContainer.Object;
                            });
                fakeBlobContainer.Setup(x => x.GetBlobReference(It.IsAny<string>())).Returns(fakeBlob.Object);
                fakeBlob.Setup(x => x.DownloadToStreamAsync(It.IsAny<Stream>(), It.IsAny<IAccessCondition>()))
                    .Callback<Stream, IAccessCondition>((x, _) => { x.WriteByte(42); })
                    .Returns(Task.FromResult(0));

                var service = CreateService(fakeBlobClient: fakeBlobClient);

                var stream = await service.GetFileAsync(folderName, "theFileName");

                Assert.Equal(42, ((MemoryStream)stream).ToArray()[0]);
            }

            [Theory]
            [FolderNamesData]
            public async Task WillReturnNullIfFileDoesNotExist(string folderName)
            {
                var fakeBlobClient = new Mock<ICloudBlobClient>();
                var fakeBlobContainer = new Mock<ICloudBlobContainer>();
                var fakeBlob = new Mock<ISimpleCloudBlob>();
                fakeBlobClient.Setup(x => x.GetContainerReference(It.IsAny<string>()))
                    .Returns<string>(
                        container =>
                            {
                                Mock<ICloudBlobContainer> blobContainer;
                                if (container == folderName)
                                {
                                    blobContainer = fakeBlobContainer;
                                }
                                else
                                {
                                    blobContainer = new Mock<ICloudBlobContainer>();
                                }
                                blobContainer.Setup(x => x.CreateIfNotExistAsync(It.IsAny<bool>())).Returns(Task.FromResult(0));
                                return blobContainer.Object;
                            });
                fakeBlobContainer.Setup(x => x.GetBlobReference(It.IsAny<string>())).Returns(fakeBlob.Object);

                fakeBlob.Setup(x => x.DownloadToStreamAsync(It.IsAny<Stream>(), It.IsAny<IAccessCondition>())).Throws(
                    new CloudBlobNotFoundException(null));
                var service = CreateService(fakeBlobClient: fakeBlobClient);

                var stream = await service.GetFileAsync(folderName, "theFileName");

                Assert.Null(stream);
            }

            [Theory]
            [FolderNamesData]
            public async Task WillSetTheStreamPositionToZero(string folderName)
            {
                var fakeBlobClient = new Mock<ICloudBlobClient>();
                var fakeBlobContainer = new Mock<ICloudBlobContainer>();
                var fakeBlob = new Mock<ISimpleCloudBlob>();
                fakeBlobClient.Setup(x => x.GetContainerReference(It.IsAny<string>()))
                    .Returns<string>(
                        container =>
                            {
                                Mock<ICloudBlobContainer> blobContainer;
                                if (container == folderName)
                                {
                                    blobContainer = fakeBlobContainer;
                                }
                                else
                                {
                                    blobContainer = new Mock<ICloudBlobContainer>();
                                }
                                blobContainer.Setup(x => x.CreateIfNotExistAsync(It.IsAny<bool>())).Returns(Task.FromResult(0));
                                return blobContainer.Object;
                            });
                fakeBlobContainer.Setup(x => x.GetBlobReference(It.IsAny<string>())).Returns(fakeBlob.Object);
                fakeBlob.Setup(x => x.DownloadToStreamAsync(It.IsAny<Stream>(), It.IsAny<IAccessCondition>()))
                        .Callback<Stream, IAccessCondition>((x, _) => { x.WriteByte(42); })
                        .Returns(Task.FromResult(0));
                var service = CreateService(fakeBlobClient: fakeBlobClient);

                var stream = await service.GetFileAsync(folderName, "theFileName");

                Assert.Equal(0, stream.Position);
            }
        }

        public class TheSaveFileMethod
        {
            [Theory]
            [FolderNamesData(includeContentTypes: true)]
            public async Task WillGetTheBlobFromTheCorrectFolderContainer(string folderName, string contentType)
            {
                var fakeBlobClient = new Mock<ICloudBlobClient>();
                var fakeBlobContainer = new Mock<ICloudBlobContainer>();
                var fakeBlob = new Mock<ISimpleCloudBlob>();
                fakeBlobClient.Setup(x => x.GetContainerReference(It.IsAny<string>()))
                    .Returns<string>(
                        container =>
                            {
                                Mock<ICloudBlobContainer> blobContainer;
                                if (container == folderName)
                                {
                                    blobContainer = fakeBlobContainer;
                                }
                                else
                                {
                                    blobContainer = new Mock<ICloudBlobContainer>();
                                }
                                blobContainer.Setup(x => x.CreateIfNotExistAsync(It.IsAny<bool>())).Returns(Task.FromResult(0));
                                return blobContainer.Object;
                            });
                fakeBlobContainer.Setup(x => x.GetBlobReference(It.IsAny<string>())).Returns(fakeBlob.Object);
                fakeBlob.Setup(x => x.Properties).Returns(Mock.Of<ICloudBlobProperties>());
                fakeBlob.Setup(x => x.Uri).Returns(new Uri("http://theUri"));
                fakeBlob.Setup(x => x.DeleteIfExistsAsync()).Returns(Task.FromResult(0));
                fakeBlob.Setup(x => x.UploadFromStreamAsync(It.IsAny<Stream>(), true)).Returns(Task.FromResult(0));
                fakeBlob.Setup(x => x.SetPropertiesAsync()).Returns(Task.FromResult(0));

                var service = CreateService(fakeBlobClient: fakeBlobClient);

                await service.SaveFileAsync(folderName, "theFileName", new MemoryStream());

                fakeBlobContainer.Verify(x => x.GetBlobReference("theFileName"));
            }

            [Fact]
            public async Task WillDeleteBlobIfItExistsAndOverwriteTrue()
            {
                var fakeBlobClient = new Mock<ICloudBlobClient>();
                var fakeBlobContainer = new Mock<ICloudBlobContainer>();
                var fakeBlob = new Mock<ISimpleCloudBlob>();
                fakeBlob.Setup(x => x.UploadFromStreamAsync(It.IsAny<Stream>(), true)).Returns(Task.FromResult(0));
                fakeBlob.Setup(x => x.SetPropertiesAsync()).Returns(Task.FromResult(0)).Verifiable();
                fakeBlobClient.Setup(x => x.GetContainerReference(It.IsAny<string>())).Returns(fakeBlobContainer.Object);
                fakeBlobContainer.Setup(x => x.GetBlobReference(It.IsAny<string>())).Returns(fakeBlob.Object);
                fakeBlobContainer.Setup(x => x.CreateIfNotExistAsync(It.IsAny<bool>())).Returns(Task.FromResult(0));
                fakeBlob.Setup(x => x.Properties).Returns(Mock.Of<ICloudBlobProperties>());
                fakeBlob.Setup(x => x.Uri).Returns(new Uri("http://theUri"));
                var service = CreateService(fakeBlobClient: fakeBlobClient);

                await service.SaveFileAsync(CoreConstants.Folders.PackagesFolderName, "theFileName", new MemoryStream());

                fakeBlob.Verify();
            }

            [Fact]
            public async Task WillThrowIfBlobExistsAndOverwriteFalse()
            {
                var fakeBlobClient = new Mock<ICloudBlobClient>();
                var fakeBlobContainer = new Mock<ICloudBlobContainer>();
                var fakeBlob = new Mock<ISimpleCloudBlob>();
                fakeBlob
                    .Setup(x => x.UploadFromStreamAsync(It.IsAny<Stream>(), false))
                    .Throws(new CloudBlobConflictException(new Exception("inner")));
                fakeBlobClient.Setup(x => x.GetContainerReference(It.IsAny<string>())).Returns(fakeBlobContainer.Object);
                fakeBlobContainer.Setup(x => x.GetBlobReference(It.IsAny<string>())).Returns(fakeBlob.Object);
                fakeBlobContainer.Setup(x => x.CreateIfNotExistAsync(It.IsAny<bool>())).Returns(Task.FromResult(0));
                fakeBlob.Setup(x => x.Properties).Returns(Mock.Of<ICloudBlobProperties>());
                fakeBlob.Setup(x => x.Uri).Returns(new Uri("http://theUri"));
                var service = CreateService(fakeBlobClient: fakeBlobClient);

                await Assert.ThrowsAsync<FileAlreadyExistsException>(async () => await service.SaveFileAsync(CoreConstants.Folders.PackagesFolderName, "theFileName", new MemoryStream(), overwrite: false));

                fakeBlob.Verify();
            }

            [Fact]
            public async Task WillUploadThePackageFileToTheBlob()
            {
                var fakeBlobClient = new Mock<ICloudBlobClient>();
                var fakeBlobContainer = new Mock<ICloudBlobContainer>();
                fakeBlobContainer.Setup(x => x.CreateIfNotExistAsync(It.IsAny<bool>())).Returns(Task.FromResult(0));
                var fakeBlob = new Mock<ISimpleCloudBlob>();
                fakeBlobClient.Setup(x => x.GetContainerReference(It.IsAny<string>())).Returns(fakeBlobContainer.Object);
                fakeBlobContainer.Setup(x => x.GetBlobReference(It.IsAny<string>())).Returns(fakeBlob.Object);
                fakeBlob.Setup(x => x.Properties).Returns(Mock.Of<ICloudBlobProperties>());
                fakeBlob.Setup(x => x.Uri).Returns(new Uri("http://theUri"));
                fakeBlob.Setup(x => x.DeleteIfExistsAsync()).Returns(Task.FromResult(0));
                fakeBlob.Setup(x => x.SetPropertiesAsync()).Returns(Task.FromResult(0));
                var service = CreateService(fakeBlobClient: fakeBlobClient);
                var fakePackageFile = new MemoryStream();
                fakeBlob.Setup(x => x.UploadFromStreamAsync(fakePackageFile, true)).Returns(Task.FromResult(0)).Verifiable();

                await service.SaveFileAsync(CoreConstants.Folders.PackagesFolderName, "theFileName", fakePackageFile);

                fakeBlob.Verify();
            }

            [Theory]
            [FolderNamesData]
            public async Task WillSetTheBlobContentType(string folderName)
            {
                var fakeBlobClient = new Mock<ICloudBlobClient>();
                var fakeBlobContainer = new Mock<ICloudBlobContainer>();
                var fakeBlob = new Mock<ISimpleCloudBlob>();
                fakeBlobClient.Setup(x => x.GetContainerReference(It.IsAny<string>()))
                    .Returns<string>(
                        container =>
                            {
                                Mock<ICloudBlobContainer> blobContainer;
                                if (container == folderName)
                                {
                                    blobContainer = fakeBlobContainer;
                                }
                                else
                                {
                                    blobContainer = new Mock<ICloudBlobContainer>();
                                }
                                blobContainer.Setup(x => x.CreateIfNotExistAsync(It.IsAny<bool>())).Returns(Task.FromResult(0));
                                return blobContainer.Object;
                            });
                fakeBlobContainer.Setup(x => x.GetBlobReference(It.IsAny<string>())).Returns(fakeBlob.Object);
                fakeBlob.Setup(x => x.Properties).Returns(Mock.Of<ICloudBlobProperties>());
                fakeBlob.Setup(x => x.Uri).Returns(new Uri("http://theUri"));
                fakeBlob.Setup(x => x.DeleteIfExistsAsync()).Returns(Task.FromResult(0));
                fakeBlob.Setup(x => x.UploadFromStreamAsync(It.IsAny<Stream>(), true)).Returns(Task.FromResult(0));
                fakeBlob.Setup(x => x.SetPropertiesAsync()).Returns(Task.FromResult(0));
                var fakeFolderInformationProvider = new Mock<ICloudBlobContainerInformationProvider>();
                const string ContentType = "some/content-type";
                fakeFolderInformationProvider
                    .Setup(fip => fip.GetContentType(folderName))
                    .Returns(ContentType);
                var service = CreateService(fakeBlobClient: fakeBlobClient, fakeFolderInformationProvider: fakeFolderInformationProvider);

                await service.SaveFileAsync(folderName, "theFileName", new MemoryStream());

                fakeFolderInformationProvider
                    .Verify(fip => fip.GetContentType(folderName), Times.Once);
                Assert.Equal(ContentType, fakeBlob.Object.Properties.ContentType);
                fakeBlob.Verify(x => x.SetPropertiesAsync());
            }

            [Theory]
            [FolderNamesData]
            public async Task WillSetTheBlobControlCacheOnPackagesFolder(string folderName)
            {
                var fakeBlobClient = new Mock<ICloudBlobClient>();
                var fakeBlobContainer = new Mock<ICloudBlobContainer>();
                fakeBlobContainer.Setup(x => x.CreateIfNotExistAsync(It.IsAny<bool>())).Returns(Task.FromResult(0));
                var fakeBlob = new Mock<ISimpleCloudBlob>();
                fakeBlobClient.Setup(x => x.GetContainerReference(It.IsAny<string>())).Returns(fakeBlobContainer.Object);
                fakeBlobContainer.Setup(x => x.GetBlobReference(It.IsAny<string>())).Returns(fakeBlob.Object);
                fakeBlob.Setup(x => x.Properties).Returns(Mock.Of<ICloudBlobProperties>());
                fakeBlob.Setup(x => x.Uri).Returns(new Uri("http://theUri"));
                fakeBlob.Setup(x => x.DeleteIfExistsAsync()).Returns(Task.FromResult(0));
                fakeBlob.Setup(x => x.SetPropertiesAsync()).Returns(Task.FromResult(0));
                var fakeFolderInformationProvider = new Mock<ICloudBlobContainerInformationProvider>();
                fakeFolderInformationProvider
                    .Setup(fip => fip.GetContentType(folderName))
                    .Returns("some/content-type");
                const string CacheControl = "cache-control";
                fakeFolderInformationProvider
                    .Setup(fip => fip.GetCacheControl(folderName))
                    .Returns(CacheControl)
                    .Verifiable();
                var service = CreateService(fakeBlobClient: fakeBlobClient, fakeFolderInformationProvider: fakeFolderInformationProvider);
                var fakePackageFile = new MemoryStream();
                fakeBlob.Setup(x => x.UploadFromStreamAsync(fakePackageFile, true)).Returns(Task.FromResult(0)).Verifiable();

                await service.SaveFileAsync(folderName, "theFileName", fakePackageFile);

                fakeBlob.Verify();
                fakeFolderInformationProvider.Verify();

                Assert.Equal(CacheControl, fakeBlob.Object.Properties.CacheControl);

                fakeBlob.Verify(x => x.SetPropertiesAsync());
            }
        }

        public class TheSaveFileWithAccessConditionMethod
        {
            [Theory]
            [FolderNamesData(includeContentTypes: true)]
            public async Task WillGetTheBlobFromTheCorrectFolderContainer(string folderName, string contentType)
            {
                var fakeBlobClient = new Mock<ICloudBlobClient>();
                var fakeBlobContainer = new Mock<ICloudBlobContainer>();
                var fakeBlob = new Mock<ISimpleCloudBlob>();
                fakeBlobClient.Setup(x => x.GetContainerReference(It.IsAny<string>()))
                    .Returns<string>(
                        container =>
                        {
                            Mock<ICloudBlobContainer> blobContainer;
                            if (container == folderName)
                            {
                                blobContainer = fakeBlobContainer;
                            }
                            else
                            {
                                blobContainer = new Mock<ICloudBlobContainer>();
                            }
                            blobContainer.Setup(x => x.CreateIfNotExistAsync(It.IsAny<bool>())).Returns(Task.FromResult(0));
                            return blobContainer.Object;
                        });
                fakeBlobContainer.Setup(x => x.GetBlobReference(It.IsAny<string>())).Returns(fakeBlob.Object);
                fakeBlob.Setup(x => x.Properties).Returns(Mock.Of<ICloudBlobProperties>());
                fakeBlob.Setup(x => x.Uri).Returns(new Uri("http://theUri"));
                fakeBlob.Setup(x => x.DeleteIfExistsAsync()).Returns(Task.FromResult(0));
                fakeBlob.Setup(x => x.UploadFromStreamAsync(It.IsAny<Stream>(), true)).Returns(Task.FromResult(0));
                fakeBlob.Setup(x => x.SetPropertiesAsync()).Returns(Task.FromResult(0));

                var service = CreateService(fakeBlobClient: fakeBlobClient);
                var accessCondition = AccessConditionWrapper.GenerateEmptyCondition();

                await service.SaveFileAsync(folderName, "theFileName", new MemoryStream(), accessConditions: null);

                fakeBlobContainer.Verify(x => x.GetBlobReference("theFileName"));
            }

            [Theory]
            [MemberData(nameof(PassesAccessConditionToBlobData))]
            public async Task PassesAccessConditionToBlob(IAccessCondition condition, string expectedIfMatchETag, string expectedIfNoneMatchETag)
            {
                var fakeBlobClient = new Mock<ICloudBlobClient>();
                var fakeBlobContainer = new Mock<ICloudBlobContainer>();
                var fakeBlob = new Mock<ISimpleCloudBlob>();

                fakeBlobClient.Setup(x => x.GetContainerReference(It.IsAny<string>())).Returns(fakeBlobContainer.Object);
                fakeBlobContainer.Setup(x => x.GetBlobReference(It.IsAny<string>())).Returns(fakeBlob.Object);
                fakeBlob.Setup(x => x.Properties).Returns(Mock.Of<ICloudBlobProperties>());

                var service = CreateService(fakeBlobClient: fakeBlobClient);

                await service.SaveFileAsync(CoreConstants.Folders.PackagesFolderName, "theFileName", new MemoryStream(), condition);

                fakeBlob.Verify(
                    b => b.UploadFromStreamAsync(
                        It.IsAny<Stream>(),
                        It.Is<IAccessCondition>(
                            c => c.IfMatchETag == expectedIfMatchETag && c.IfNoneMatchETag == expectedIfNoneMatchETag)),
                    Times.Once);
            }

            public static IEnumerable<object[]> PassesAccessConditionToBlobData()
            {
                // If no condition is provided, the upload should default to a "if not exists" condition.
                yield return new object[]
                {
                    /* condition: */ null,
                    /* expectedIfMatchETag: */ null,
                    /* expectedIfNoneMatchETag: */ "*"
                };

                yield return new object[]
                {
                    /* condition: */ AccessConditionWrapper.GenerateEmptyCondition(),
                    /* expectedIfMatchETag: */ null,
                    /* expectedIfNoneMatchETag: */ null
                };

                yield return new object[]
                {
                    /* condition: */ AccessConditionWrapper.GenerateIfMatchCondition("foo-bar"),
                    /* expectedIfMatchETag: */ "foo-bar",
                    /* expectedIfNoneMatchETag: */ null
                };

                yield return new object[]
                {
                    /* condition: */ AccessConditionWrapper.GenerateIfNotExistsCondition(),
                    /* expectedIfMatchETag: */ null,
                    /* expectedIfNoneMatchETag: */ "*"
                };
            }

            [Fact]
            public async Task ThrowsIfBlobUploadThrowsFileAlreadyExistsException()
            {
                var fakeBlobClient = new Mock<ICloudBlobClient>();
                var fakeBlobContainer = new Mock<ICloudBlobContainer>();
                var fakeBlob = new Mock<ISimpleCloudBlob>();

                fakeBlobClient.Setup(x => x.GetContainerReference(It.IsAny<string>())).Returns(fakeBlobContainer.Object);
                fakeBlobContainer.Setup(x => x.GetBlobReference(It.IsAny<string>())).Returns(fakeBlob.Object);
                fakeBlob.Setup(x => x.Properties).Returns(Mock.Of<ICloudBlobProperties>());
                fakeBlob
                    .Setup(x => x.UploadFromStreamAsync(It.IsAny<Stream>(), It.IsAny<IAccessCondition>()))
                    .Throws(new CloudBlobConflictException(new Exception("inner")));

                var service = CreateService(fakeBlobClient: fakeBlobClient);

                await Assert.ThrowsAsync<FileAlreadyExistsException>(
                    () => service.SaveFileAsync(
                        CoreConstants.Folders.PackagesFolderName,
                        "theFileName",
                        new MemoryStream(),
                        AccessConditionWrapper.GenerateIfNotExistsCondition()));
            }

            [Theory]
            [FolderNamesData]
            public async Task WillSetTheBlobContentType(string folderName)
            {
                var fakeBlobClient = new Mock<ICloudBlobClient>();
                var fakeBlobContainer = new Mock<ICloudBlobContainer>();
                var fakeBlob = new Mock<ISimpleCloudBlob>();
                fakeBlobClient.Setup(x => x.GetContainerReference(It.IsAny<string>()))
                    .Returns<string>(
                        container =>
                        {
                            Mock<ICloudBlobContainer> blobContainer;
                            if (container == folderName)
                            {
                                blobContainer = fakeBlobContainer;
                            }
                            else
                            {
                                blobContainer = new Mock<ICloudBlobContainer>();
                            }
                            blobContainer.Setup(x => x.CreateIfNotExistAsync(It.IsAny<bool>())).Returns(Task.FromResult(0));
                            return blobContainer.Object;
                        });
                fakeBlobContainer.Setup(x => x.GetBlobReference(It.IsAny<string>())).Returns(fakeBlob.Object);
                fakeBlob.Setup(x => x.Properties).Returns(Mock.Of<ICloudBlobProperties>());
                fakeBlob.Setup(x => x.Uri).Returns(new Uri("http://theUri"));
                fakeBlob.Setup(x => x.DeleteIfExistsAsync()).Returns(Task.FromResult(0));
                fakeBlob.Setup(x => x.UploadFromStreamAsync(It.IsAny<Stream>(), true)).Returns(Task.FromResult(0));
                fakeBlob.Setup(x => x.SetPropertiesAsync()).Returns(Task.FromResult(0));
                var fakeFolderInformationProvider = new Mock<ICloudBlobContainerInformationProvider>();
                const string ContentType = "some/content-type";
                fakeFolderInformationProvider
                    .Setup(fip => fip.GetContentType(folderName))
                    .Returns(ContentType);
                var service = CreateService(fakeBlobClient: fakeBlobClient, fakeFolderInformationProvider: fakeFolderInformationProvider);

                await service.SaveFileAsync(folderName, "theFileName", new MemoryStream(), AccessConditionWrapper.GenerateIfNotExistsCondition());

                fakeFolderInformationProvider
                    .Verify(fip => fip.GetContentType(folderName), Times.Once);
                Assert.Equal(ContentType, fakeBlob.Object.Properties.ContentType);
                fakeBlob.Verify(x => x.SetPropertiesAsync());
            }
        }

        public class TheGetFileUriAsyncMethod
        {
            private const string folderName = "theFolderName";
            private const string fileName = "theFileName";

            [Fact]
            public async Task WillThrowIfFolderIsNull()
            {
                var service = CreateService();

                var ex = await Assert.ThrowsAsync<ArgumentNullException>(() => service.GetFileUriAsync(null, fileName));
                Assert.Equal("folderName", ex.ParamName);
            }

            [Fact]
            public async Task WillThrowIfFilenameIsNull()
            {
                var service = CreateService();

                var ex = await Assert.ThrowsAsync<ArgumentNullException>(() => service.GetFileUriAsync(folderName, null));
                Assert.Equal("fileName", ex.ParamName);
            }

            [Fact]
            public async Task WillAlwaysReturnValidUri()
            {
                var containerName = CoreConstants.Folders.ValidationFolderName;
                var expectedUri = $"http://example.com/{CoreConstants.Folders.ValidationFolderName}/{fileName}";

                var setupResult = Setup(containerName, fileName);
                var fakeBlobClient = setupResult.Item1;

                var service = CreateService(fakeBlobClient);

                var uri = await service.GetFileUriAsync(containerName, fileName);

                Assert.Equal(expectedUri, uri.AbsoluteUri);
            }

            private static Tuple<Mock<ICloudBlobClient>, Mock<ISimpleCloudBlob>, Uri> Setup(string folderName, string fileName)
            {
                var fakeBlobClient = new Mock<ICloudBlobClient>();
                var fakeContainer = new Mock<ICloudBlobContainer>();
                fakeBlobClient
                    .Setup(bc => bc.GetContainerReference(folderName))
                    .Returns(fakeContainer.Object)
                    .Callback(() => { int i = 0; i = i + 1; });
                var fakeBlob = new Mock<ISimpleCloudBlob>();
                fakeContainer.Setup(c => c.GetBlobReference(fileName)).Returns(fakeBlob.Object);

                var blobUri = new Uri($"http://example.com/{folderName}/{fileName}");

                fakeBlob.SetupGet(b => b.Uri).Returns(blobUri);

                return Tuple.Create(fakeBlobClient, fakeBlob, blobUri);
            }
        }

        public class TheGetPrivilegedFileUriAsyncMethod
        {
            private const string folderName = "theFolderName";
            private const string fileName = "theFileName";
            private const string signature = "?secret=42";

            [Fact]
            public async Task WillThrowIfFolderIsNull()
            {
                var service = CreateService();

                var ex = await Assert.ThrowsAsync<ArgumentNullException>(() => service.GetPrivilegedFileUriAsync(
                    null,
                    fileName,
                    FileUriPermissions.Read,
                    DateTimeOffset.UtcNow.AddHours(3)));
                Assert.Equal("folderName", ex.ParamName);
            }

            [Fact]
            public async Task WillThrowIfFilenameIsNull()
            {
                var service = CreateService();

                var ex = await Assert.ThrowsAsync<ArgumentNullException>(() => service.GetPrivilegedFileUriAsync(
                    folderName,
                    null,
                    FileUriPermissions.Read,
                    DateTimeOffset.UtcNow.AddHours(3)));
                Assert.Equal("fileName", ex.ParamName);
            }

            [Fact]
            public async Task WillThrowIfEndOfAccessIsInThePast()
            {
                var service = CreateService();

                DateTimeOffset inThePast = DateTimeOffset.UtcNow.AddSeconds(-1);
                var ex = await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => service.GetPrivilegedFileUriAsync(
                    folderName,
                    fileName,
                    FileUriPermissions.Read,
                    inThePast));
                Assert.Equal("endOfAccess", ex.ParamName);
            }

            [Theory]
            [InlineData(CoreConstants.Folders.ValidationFolderName, "http://example.com/" + CoreConstants.Folders.ValidationFolderName + "/" + fileName + signature)]
            [InlineData(CoreConstants.Folders.PackagesFolderName, "http://example.com/" + CoreConstants.Folders.PackagesFolderName + "/" + fileName + signature)]
            public async Task WillAlwaysUseSasTokenDependingOnContainerAvailability(string containerName, string expectedUri)
            {
                var setupResult = Setup(containerName, fileName);
                var fakeBlobClient = setupResult.Item1;
                var fakeBlob = setupResult.Item2;
                var blobUri = setupResult.Item3;

                fakeBlob
                    .Setup(b => b.GetSharedAccessSignature(FileUriPermissions.Read, It.IsAny<DateTimeOffset>()))
                    .ReturnsAsync(signature);
                var service = CreateService(fakeBlobClient);

                var uri = await service.GetPrivilegedFileUriAsync(
                    containerName,
                    fileName,
                    FileUriPermissions.Read,
                    DateTimeOffset.Now.AddHours(3));

                Assert.Equal(expectedUri, uri.AbsoluteUri);
            }

            [Fact]
            public async Task WillPassTheEndOfAccessTimestampFurther()
            {
                const string folderName = CoreConstants.Folders.ValidationFolderName;
                const string fileName = "theFileName";
                const string signature = "?secret=42";
                DateTimeOffset endOfAccess = DateTimeOffset.Now.AddHours(3);
                var setupResult = Setup(folderName, fileName);
                var fakeBlobClient = setupResult.Item1;
                var fakeBlob = setupResult.Item2;
                var blobUri = setupResult.Item3;

                fakeBlob
                    .Setup(b => b.GetSharedAccessSignature(
                        FileUriPermissions.Read | FileUriPermissions.Delete,
                        endOfAccess))
                    .ReturnsAsync(signature)
                    .Verifiable();

                var service = CreateService(fakeBlobClient);

                var uri = await service.GetPrivilegedFileUriAsync(
                    folderName,
                    fileName,
                    FileUriPermissions.Read | FileUriPermissions.Delete,
                    endOfAccess);

                string expectedUri = new Uri(blobUri, signature).AbsoluteUri;
                Assert.Equal(expectedUri, uri.AbsoluteUri);
                fakeBlob.Verify(
                    b => b.GetSharedAccessSignature(FileUriPermissions.Read | FileUriPermissions.Delete, endOfAccess),
                    Times.Once);
                fakeBlob.Verify(
                    b => b.GetSharedAccessSignature(It.IsAny<FileUriPermissions>(),
                    It.IsAny<DateTimeOffset>()), Times.Once);
            }

            private static Tuple<Mock<ICloudBlobClient>, Mock<ISimpleCloudBlob>, Uri> Setup(string folderName, string fileName)
            {
                var fakeBlobClient = new Mock<ICloudBlobClient>();
                var fakeContainer = new Mock<ICloudBlobContainer>();
                fakeBlobClient
                    .Setup(bc => bc.GetContainerReference(folderName))
                    .Returns(fakeContainer.Object)
                    .Callback(() => { int i = 0; i = i + 1; });
                var fakeBlob = new Mock<ISimpleCloudBlob>();
                fakeContainer.Setup(c => c.GetBlobReference(fileName)).Returns(fakeBlob.Object);

                var blobUri = new Uri($"http://example.com/{folderName}/{fileName}");

                fakeBlob.SetupGet(b => b.Uri).Returns(blobUri);

                return Tuple.Create(fakeBlobClient, fakeBlob, blobUri);
            }
        }

        public class TheGetFileReadUriAsyncMethod
        {
            private const string folderName = "theFolderName";
            private const string fileName = "theFileName";
            private const string signature = "?secret=42";

            [Fact]
            public async Task WillThrowIfFolderIsNull()
            {
                var service = CreateService();

                var ex = await Assert.ThrowsAsync<ArgumentNullException>(() => service.GetFileReadUriAsync(null, fileName, DateTimeOffset.UtcNow.AddHours(3)));
                Assert.Equal("folderName", ex.ParamName);
            }

            [Fact]
            public async Task WillThrowIfFilenameIsNull()
            {
                var service = CreateService();

                var ex = await Assert.ThrowsAsync<ArgumentNullException>(() => service.GetFileReadUriAsync(folderName, null, DateTimeOffset.UtcNow.AddHours(3)));
                Assert.Equal("fileName", ex.ParamName);
            }

            [Fact]
            public async Task WillThrowIfEndOfAccessIsInThePastForNonPublicContainer()
            {
                var setupResult = Setup(folderName, fileName);
                var fakeFolderInformationProvider = new Mock<ICloudBlobContainerInformationProvider>();
                fakeFolderInformationProvider
                    .Setup(fip => fip.IsPublicContainer(It.IsAny<string>()))
                    .Returns(false);
                var service = CreateService(setupResult.Item1, fakeFolderInformationProvider);
                DateTimeOffset inThePast = DateTimeOffset.UtcNow.AddSeconds(-1);
                
                var ex = await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => service.GetFileReadUriAsync(folderName, fileName, inThePast));
                
                Assert.Equal("endOfAccess", ex.ParamName);
            }

            [Theory]
            [InlineData(false, fileName + signature)]
            [InlineData(true, fileName)]
            public async Task WillUseSasTokenDependingOnContainerAvailability(bool isPublicContainer, string expectedUriPostfix)
            {
                const string containerName = "someContainerName";
                const string uriPrefix = "http://example.com/" + containerName + "/";
                var expectedUri = uriPrefix + expectedUriPostfix;
                var setupResult = Setup(containerName, fileName);
                var fakeBlobClient = setupResult.Item1;
                var fakeBlob = setupResult.Item2;
                var blobUri = setupResult.Item3;

                fakeBlob
                    .Setup(b => b.GetSharedAccessSignature(FileUriPermissions.Read, It.IsAny<DateTimeOffset>()))
                    .ReturnsAsync(signature);
                var fakeFolderInformationProvider = new Mock<ICloudBlobContainerInformationProvider>();
                fakeFolderInformationProvider
                    .Setup(fip => fip.IsPublicContainer(containerName))
                    .Returns(isPublicContainer);
                var service = CreateService(fakeBlobClient, fakeFolderInformationProvider);

                var uri = await service.GetFileReadUriAsync(containerName, fileName, DateTimeOffset.Now.AddHours(3));

                Assert.Equal(expectedUri, uri.AbsoluteUri);
            }

            [Fact]
            public async Task WillThrowIfNoEndOfAccessSpecifiedForNonPublicContainer()
            {
                var setupResult = Setup(CoreConstants.Folders.ValidationFolderName, fileName);
                var fakeFolderInformationProvider = new Mock<ICloudBlobContainerInformationProvider>();
                fakeFolderInformationProvider
                    .Setup(fip => fip.IsPublicContainer(It.IsAny<string>()))
                    .Returns(false);
                var service = CreateService(setupResult.Item1, fakeFolderInformationProvider);

                var ex = await Assert.ThrowsAsync<ArgumentNullException>(() => service.GetFileReadUriAsync(CoreConstants.Folders.ValidationFolderName, fileName, null));
                Assert.Equal("endOfAccess", ex.ParamName);
            }

            [Fact]
            public async Task WillNotThrowIfNoEndOfAccessSpecifiedForPublicContainer()
            {
                const string packagesFolderName = CoreConstants.Folders.PackagesFolderName;
                var setupResult = Setup(packagesFolderName, fileName);
                var fakeFolderInformationProvider = new Mock<ICloudBlobContainerInformationProvider>();
                fakeFolderInformationProvider
                    .Setup(fip => fip.IsPublicContainer(packagesFolderName))
                    .Returns(true);
                var service = CreateService(setupResult.Item1, fakeFolderInformationProvider);

                var ex = await Record.ExceptionAsync(() => service.GetFileReadUriAsync(packagesFolderName, fileName, null));

                Assert.Null(ex);
            }

            [Fact]
            public async Task WillPassTheEndOfAccessTimestampFurther()
            {
                const string folderName = CoreConstants.Folders.ValidationFolderName;
                const string signature = "?secret=42";
                DateTimeOffset endOfAccess = DateTimeOffset.Now.AddHours(3);
                var setupResult = Setup(folderName, fileName);
                var fakeBlobClient = setupResult.Item1;
                var fakeBlob = setupResult.Item2;
                var blobUri = setupResult.Item3;

                fakeBlob
                    .Setup(b => b.GetSharedAccessSignature(FileUriPermissions.Read, endOfAccess))
                    .ReturnsAsync(signature)
                    .Verifiable();

                var service = CreateService(fakeBlobClient);

                var uri = await service.GetFileReadUriAsync(folderName, fileName, endOfAccess);

                string expectedUri = new Uri(blobUri, signature).AbsoluteUri;
                Assert.Equal(expectedUri, uri.AbsoluteUri);
                fakeBlob.Verify(b => b.GetSharedAccessSignature(FileUriPermissions.Read, endOfAccess), Times.Once);
                fakeBlob.Verify(b => b.GetSharedAccessSignature(It.IsAny<FileUriPermissions>(), It.IsAny<DateTimeOffset>()), Times.Once);
            }

            private static Tuple<Mock<ICloudBlobClient>, Mock<ISimpleCloudBlob>, Uri> Setup(string folderName, string fileName)
            {
                var fakeBlobClient = new Mock<ICloudBlobClient>();
                var fakeContainer = new Mock<ICloudBlobContainer>();
                fakeBlobClient
                    .Setup(bc => bc.GetContainerReference(folderName))
                    .Returns(fakeContainer.Object)
                    .Callback(() => { int i = 0; i = i + 1; });
                var fakeBlob = new Mock<ISimpleCloudBlob>();
                fakeContainer.Setup(c => c.GetBlobReference(fileName)).Returns(fakeBlob.Object);

                var blobUri = new Uri($"http://example.com/{folderName}/{fileName}");

                fakeBlob.SetupGet(b => b.Uri).Returns(blobUri);

                return Tuple.Create(fakeBlobClient, fakeBlob, blobUri);
            }
        }

        public class TheCopyFileAsyncMethod
        {
            private string _srcFolderName;
            private string _srcFileName;
            private string _srcETag;
            private Uri _srcUri;
            private Uri _destUri;
            private Mock<ICloudBlobProperties> _srcProperties;
            private IDictionary<string, string> _srcMetadata;
            private string _destFolderName;
            private string _destFileName;
            private string _destETag;
            private Mock<ICloudBlobProperties> _destProperties;
            private IDictionary<string, string> _destMetadata;
            private string _metadataSha512HashAlgorithmId;
            private Mock<ICloudBlobCopyState> _destCopyState;
            private Mock<ICloudBlobClient> _blobClient;
            private Mock<ICloudBlobContainer> _srcContainer;
            private Mock<ICloudBlobContainer> _destContainer;
            private Mock<ISimpleCloudBlob> _srcBlobMock;
            private Mock<ISimpleCloudBlob> _destBlobMock;
            private CloudBlobCoreFileStorageService _target;

            public TheCopyFileAsyncMethod()
            {
                _srcFolderName = "validation";
                _srcFileName = "4b6f16cc-7acd-45eb-ac21-33f0d927ec14/nuget.versioning.4.5.0.nupkg";
                _srcETag = "\"src-etag\"";
                _srcUri = new Uri("https://srcexample/srcpackage.nupkg");
                _srcProperties = new Mock<ICloudBlobProperties>();
                _destFolderName = "packages";
                _destFileName = "nuget.versioning.4.5.0.nupkg";
                _destETag = "\"dest-etag\"";
                _destUri = new Uri("https://destexample/destpackage.nupkg");
                _destProperties = new Mock<ICloudBlobProperties>();
                _destCopyState = new Mock<ICloudBlobCopyState>();
                SetDestCopyStatus(CloudBlobCopyStatus.Success);
                _metadataSha512HashAlgorithmId = CoreConstants.Sha512HashAlgorithmId;

                _srcMetadata = new Dictionary<string, string>();
                _destMetadata = new Dictionary<string, string>();
                _blobClient = new Mock<ICloudBlobClient>();
                _srcContainer = new Mock<ICloudBlobContainer>();
                _destContainer = new Mock<ICloudBlobContainer>();
                _srcBlobMock = new Mock<ISimpleCloudBlob>();
                _destBlobMock = new Mock<ISimpleCloudBlob>();
                _blobClient
                    .Setup(x => x.GetContainerReference(_srcFolderName))
                    .Returns(() => _srcContainer.Object);
                _blobClient
                    .Setup(x => x.GetContainerReference(_destFolderName))
                    .Returns(() => _destContainer.Object);
                _srcContainer
                    .Setup(x => x.GetBlobReference(_srcFileName))
                    .Returns(() => _srcBlobMock.Object);
                _destContainer
                    .Setup(x => x.GetBlobReference(_destFileName))
                    .Returns(() => _destBlobMock.Object);
                _srcBlobMock
                    .Setup(x => x.Name)
                    .Returns(() => _srcFileName);
                _srcBlobMock
                    .Setup(x => x.ETag)
                    .Returns(() => _srcETag);
                _srcBlobMock
                    .Setup(x => x.Properties)
                    .Returns(() => _srcProperties.Object);
                _srcBlobMock
                    .Setup(x => x.Metadata)
                    .Returns(() => _srcMetadata);
                _srcBlobMock
                    .Setup(x => x.Uri)
                    .Returns(() => _srcUri);
                _destBlobMock
                    .Setup(x => x.ETag)
                    .Returns(() => _destETag);
                _destBlobMock
                    .Setup(x => x.Properties)
                    .Returns(() => _destProperties.Object);
                _destBlobMock
                    .Setup(x => x.CopyState)
                    .Returns(() => _destCopyState.Object);
                _destBlobMock
                    .Setup(x => x.Metadata)
                    .Returns(() => _destMetadata);
                _destBlobMock
                    .Setup(x => x.Uri)
                    .Returns(() => _destUri);

                _target = CreateService(fakeBlobClient: _blobClient);
            }

            [Fact]
            public async Task WillCopyBlobFromSourceUri()
            {
                // Arrange
                _blobClient
                    .Setup(x => x.GetBlobFromUri(It.IsAny<Uri>()))
                    .Returns(_srcBlobMock.Object);

                _destBlobMock
                    .Setup(x => x.StartCopyAsync(It.IsAny<ISimpleCloudBlob>(), It.IsAny<IAccessCondition>(), It.IsAny<IAccessCondition>()))
                    .Returns(Task.FromResult(0))
                    .Callback<ISimpleCloudBlob, IAccessCondition, IAccessCondition>((_, __, ___) =>
                    {
                        SetDestCopyStatus(CloudBlobCopyStatus.Success);
                    });

                // Act
                await _target.CopyFileAsync(
                    _srcUri,
                    _destFolderName,
                    _destFileName,
                    AccessConditionWrapper.GenerateIfNotExistsCondition());

                // Assert
                _destBlobMock.Verify(
                    x => x.StartCopyAsync(_srcBlobMock.Object, It.IsAny<IAccessCondition>(), It.IsAny<IAccessCondition>()),
                    Times.Once);
                _destBlobMock.Verify(
                    x => x.StartCopyAsync(It.IsAny<ISimpleCloudBlob>(), It.IsAny<IAccessCondition>(), It.IsAny<IAccessCondition>()),
                    Times.Once);
                _blobClient.Verify(
                    x => x.GetBlobFromUri(_srcUri),
                    Times.Once);
            }

            [Fact]
            public async Task WillCopyTheFileIfDestinationDoesNotExist()
            {
                // Arrange
                IAccessCondition srcAccessCondition = null;
                IAccessCondition destAccessCondition = null;
                ISimpleCloudBlob srcBlob = null;

                _destBlobMock
                    .Setup(x => x.StartCopyAsync(It.IsAny<ISimpleCloudBlob>(), It.IsAny<IAccessCondition>(), It.IsAny<IAccessCondition>()))
                    .Returns(Task.FromResult(0))
                    .Callback<ISimpleCloudBlob, IAccessCondition, IAccessCondition>((b, s, d) =>
                    {
                        srcBlob = b;
                        srcAccessCondition = s;
                        destAccessCondition = d;
                    });

                // Act
                await _target.CopyFileAsync(
                    _srcFolderName,
                    _srcFileName,
                    _destFolderName,
                    _destFileName,
                    AccessConditionWrapper.GenerateIfNotExistsCondition());

                // Assert
                _destBlobMock.Verify(
                    x => x.StartCopyAsync(It.IsAny<ISimpleCloudBlob>(), It.IsAny<IAccessCondition>(), It.IsAny<IAccessCondition>()),
                    Times.Once);
                Assert.Equal(_srcFileName, srcBlob.Name);
                Assert.Equal(_srcETag, srcAccessCondition.IfMatchETag);
                Assert.Equal("*", destAccessCondition.IfNoneMatchETag);
            }

            [Fact]
            public async Task WillThrowFileAlreadyExistsExceptionForConflict()
            {
                // Arrange
                _destBlobMock
                    .Setup(x => x.StartCopyAsync(It.IsAny<ISimpleCloudBlob>(), It.IsAny<IAccessCondition>(), It.IsAny<IAccessCondition>()))
                    .Throws(new CloudBlobConflictException(null));

                // Act & Assert
                await Assert.ThrowsAsync<FileAlreadyExistsException>(
                    () => _target.CopyFileAsync(
                        _srcFolderName,
                        _srcFileName,
                        _destFolderName,
                        _destFileName,
                        AccessConditionWrapper.GenerateIfNotExistsCondition()));
            }

            [Fact]
            public async Task WillCopyTheFileIfDestinationHasFailedCopy()
            {
                // Arrange
                IAccessCondition srcAccessCondition = null;
                IAccessCondition destAccessCondition = null;
                ISimpleCloudBlob srcBlob = null;

                SetDestCopyStatus(CloudBlobCopyStatus.Failed);

                _destBlobMock
                    .Setup(x => x.StartCopyAsync(It.IsAny<ISimpleCloudBlob>(), It.IsAny<IAccessCondition>(), It.IsAny<IAccessCondition>()))
                    .Returns(Task.FromResult(0))
                    .Callback<ISimpleCloudBlob, IAccessCondition, IAccessCondition>((b, s, d) =>
                    {
                        srcBlob = b;
                        srcAccessCondition = s;
                        destAccessCondition = d;
                        SetDestCopyStatus(CloudBlobCopyStatus.Pending);
                    });

                _destBlobMock
                    .Setup(x => x.ExistsAsync())
                    .ReturnsAsync(true);

                var numCalls = 0;
                _destBlobMock
                    .Setup(x => x.FetchAttributesAsync())
                    .Returns(Task.FromResult(0))
                    .Callback(() =>
                    {
                        if (++numCalls == 2)
                        {
                            SetDestCopyStatus(CloudBlobCopyStatus.Success);
                        } 
                    });

                // Act
                var srcETag = await _target.CopyFileAsync(
                    _srcFolderName,
                    _srcFileName,
                    _destFolderName,
                    _destFileName,
                    AccessConditionWrapper.GenerateIfNotExistsCondition());

                // Assert
                _destBlobMock.Verify(
                    x => x.StartCopyAsync(It.IsAny<ISimpleCloudBlob>(), It.IsAny<IAccessCondition>(), It.IsAny<IAccessCondition>()),
                    Times.Once);
                Assert.Equal(_srcETag, srcETag);
                Assert.Equal(_srcFileName, srcBlob.Name);
                Assert.Equal(_srcETag, srcAccessCondition.IfMatchETag);
                Assert.Equal(_destETag, destAccessCondition.IfMatchETag);
            }

            [Fact]
            public async Task WillDefaultToIfNotExists()
            {
                // Arrange
                IAccessCondition srcAccessCondition = null;
                IAccessCondition destAccessCondition = null;
                ISimpleCloudBlob srcBlob = null;
                _destBlobMock
                    .Setup(x => x.StartCopyAsync(It.IsAny<ISimpleCloudBlob>(), It.IsAny<IAccessCondition>(), It.IsAny<IAccessCondition>()))
                    .Returns(Task.FromResult(0))
                    .Callback<ISimpleCloudBlob, IAccessCondition, IAccessCondition>((b, s, d) =>
                    {
                        srcBlob = b;
                        srcAccessCondition = s;
                        destAccessCondition = d;
                        SetDestCopyStatus(CloudBlobCopyStatus.Success);
                    });

                // Act
                await _target.CopyFileAsync(
                    _srcFolderName,
                    _srcFileName,
                    _destFolderName,
                    _destFileName,
                    destAccessCondition: null);

                // Assert
                _destBlobMock.Verify(
                    x => x.StartCopyAsync(It.IsAny<ISimpleCloudBlob>(), It.IsAny<IAccessCondition>(), It.IsAny<IAccessCondition>()),
                    Times.Once);
                Assert.Null(destAccessCondition.IfMatchETag);
                Assert.Equal("*", destAccessCondition.IfNoneMatchETag);
            }

            [Fact]
            public async Task UsesProvidedMatchETag()
            {
                // Arrange
                IAccessCondition srcAccessCondition = null;
                IAccessCondition destAccessCondition = null;
                ISimpleCloudBlob srcBlob = null;
                _destBlobMock
                    .Setup(x => x.StartCopyAsync(It.IsAny<ISimpleCloudBlob>(), It.IsAny<IAccessCondition>(), It.IsAny<IAccessCondition>()))
                    .Returns(Task.FromResult(0))
                    .Callback<ISimpleCloudBlob, IAccessCondition, IAccessCondition>((b, s, d) =>
                    {
                        srcBlob = b;
                        srcAccessCondition = s;
                        destAccessCondition = d;
                        SetDestCopyStatus(CloudBlobCopyStatus.Success);
                    });

                // Act
                await _target.CopyFileAsync(
                    _srcFolderName,
                    _srcFileName,
                    _destFolderName,
                    _destFileName,
                    AccessConditionWrapper.GenerateIfMatchCondition("etag!"));

                // Assert
                _destBlobMock.Verify(
                    x => x.StartCopyAsync(It.IsAny<ISimpleCloudBlob>(), It.IsAny<IAccessCondition>(), It.IsAny<IAccessCondition>()),
                    Times.Once);
                Assert.Equal("etag!", destAccessCondition.IfMatchETag);
                Assert.Null(destAccessCondition.IfNoneMatchETag);
            }

            [Fact]
            public async Task NoOpsIfPackageLengthAndHashMatch()
            {
                // Arrange
                SetBlobContentSha512(_srcMetadata, "mwgwUC0MwohHxgMmvQzO7A==");
                SetBlobLength(_srcProperties, 42);
                SetBlobContentSha512(_destMetadata, _srcMetadata[_metadataSha512HashAlgorithmId]);
                SetBlobLength(_destProperties, _srcProperties.Object.Length);

                _destBlobMock
                    .Setup(x => x.ExistsAsync())
                    .ReturnsAsync(true);

                // Act
                await _target.CopyFileAsync(
                    _srcFolderName,
                    _srcFileName,
                    _destFolderName,
                    _destFileName,
                    AccessConditionWrapper.GenerateIfNotExistsCondition());

                // Assert
                _destBlobMock.Verify(
                    x => x.StartCopyAsync(It.IsAny<ISimpleCloudBlob>(), It.IsAny<IAccessCondition>(), It.IsAny<IAccessCondition>()),
                    Times.Never);
            }

            [Theory]
            [InlineData("mwgwUC0MwohHxgMmvQzO7A==", "mwgwUC0MwohHxgMmvQzO7A===", 42, 42)]
            [InlineData("mwgwUC0MwohHxgMmvQzO7A==", "mwgwUC0MwohHxgMmvQzO7A==", 42, 43)]
            [InlineData("mwgwUC0MwohHxgMmvQzO7A==", null, 42, 42)]
            [InlineData(null, "mwgwUC0MwohHxgMmvQzO7A==", 42, 42)]
            [InlineData(null, null, 42, 42)]
            public async Task OpsIfPackageLengthAndHashNotMatch(string srcMetadataSha512, string destMetadataSha512, int srcPropertiesLength, int destPropertiesLength)
            {
                // Arrange
                if (srcMetadataSha512 != null)
                {
                    SetBlobContentSha512(_srcMetadata, srcMetadataSha512);
                }
                SetBlobLength(_srcProperties, srcPropertiesLength);

                if (destMetadataSha512 != null)
                {
                    SetBlobContentSha512(_destMetadata, destMetadataSha512);
                }
                SetBlobLength(_destProperties, destPropertiesLength);

                _destBlobMock
                    .Setup(x => x.ExistsAsync())
                    .ReturnsAsync(true);

                // Act
                await _target.CopyFileAsync(
                    _srcFolderName,
                    _srcFileName,
                    _destFolderName,
                    _destFileName,
                    AccessConditionWrapper.GenerateIfNotExistsCondition());

                // Assert
                _destBlobMock.Verify(
                    x => x.StartCopyAsync(It.IsAny<ISimpleCloudBlob>(), It.IsAny<IAccessCondition>(), It.IsAny<IAccessCondition>()),
                    Times.Once);
            }

            [Fact]
            public async Task ThrowsIfCopyOperationFails()
            {
                // Arrange
                _destBlobMock
                    .Setup(x => x.StartCopyAsync(It.IsAny<ISimpleCloudBlob>(), It.IsAny<IAccessCondition>(), It.IsAny<IAccessCondition>()))
                    .Returns(Task.FromResult(0))
                    .Callback<ISimpleCloudBlob, IAccessCondition, IAccessCondition>((_, __, ___) =>
                    {
                        SetDestCopyStatus(CloudBlobCopyStatus.Failed);
                    });

                // Act & Assert
                var ex = await Assert.ThrowsAsync<CloudBlobStorageException>(
                    () => _target.CopyFileAsync(
                        _srcFolderName,
                        _srcFileName,
                        _destFolderName,
                        _destFileName,
                        AccessConditionWrapper.GenerateIfNotExistsCondition()));
                Assert.Contains("The blob copy operation had copy status Failed", ex.Message);
            }

            private void SetDestCopyStatus(CloudBlobCopyStatus copyStatus)
            {
                _destCopyState.SetupGet(x => x.Status).Returns(copyStatus);
            }

            private void SetBlobLength(Mock<ICloudBlobProperties> properties, long length)
            {
                properties.SetupGet(x => x.Length).Returns(length);
            }

            private void SetBlobContentSha512(IDictionary<string, string> metadata, string contentSha512)
            {
                metadata.Add(_metadataSha512HashAlgorithmId, contentSha512);
            }
        }

        public class TheSetMetadataAsyncMethod
        {
            private const string _content = "peach";

            private readonly Mock<ICloudBlobClient> _blobClient;
            private readonly Mock<ICloudBlobContainer> _blobContainer;
            private readonly Mock<ISimpleCloudBlob> _blob;
            private readonly CloudBlobCoreFileStorageService _service;

            public TheSetMetadataAsyncMethod()
            {
                _blobClient = new Mock<ICloudBlobClient>();
                _blobContainer = new Mock<ICloudBlobContainer>();
                _blob = new Mock<ISimpleCloudBlob>();

                _blobClient.Setup(x => x.GetContainerReference(It.IsAny<string>()))
                    .Returns(_blobContainer.Object);
                _blobContainer.Setup(x => x.CreateIfNotExistAsync(It.IsAny<bool>()))
                    .Returns(Task.FromResult(0));
                _blobContainer.Setup(x => x.GetBlobReference(It.IsAny<string>()))
                    .Returns(_blob.Object);

                _service = CreateService(fakeBlobClient: _blobClient);
            }

            [Fact]
            public async Task WhenLazyStreamRead_ReturnsContent()
            {
                _blob.Setup(x => x.DownloadToStreamAsync(It.IsAny<Stream>(), It.IsAny<IAccessCondition>()))
                    .Callback<Stream, IAccessCondition>((stream, _) =>
                    {
                        using (var writer = new StreamWriter(stream, Encoding.UTF8, bufferSize: 4096, leaveOpen: true))
                        {
                            writer.Write(_content);
                        }
                    })
                    .Returns(Task.FromResult(0));

                await _service.SetMetadataAsync(
                    folderName: CoreConstants.Folders.PackagesFolderName,
                    fileName: "a",
                    updateMetadataAsync: async (lazyStream, metadata) =>
                    {
                        using (var stream = await lazyStream.Value)
                        using (var reader = new StreamReader(stream))
                        {
                            Assert.Equal(_content, reader.ReadToEnd());
                        }

                        return false;
                    });

                _blob.VerifyAll();
                _blobContainer.VerifyAll();
                _blobClient.VerifyAll();
            }

            [Fact]
            public async Task WhenReturnValueIsFalse_MetadataChangesAreNotPersisted()
            {
                _blob.SetupGet(x => x.Metadata)
                    .Returns(new Dictionary<string, string>());

                await _service.SetMetadataAsync(
                    folderName: CoreConstants.Folders.PackagesFolderName,
                    fileName: "a",
                    updateMetadataAsync: (lazyStream, metadata) =>
                    {
                        Assert.NotNull(metadata);

                        return Task.FromResult(false);
                    });

                _blob.VerifyAll();
                _blobContainer.VerifyAll();
                _blobClient.VerifyAll();
            }

            [Fact]
            public async Task WhenReturnValueIsTrue_MetadataChangesAreNotPersisted()
            {
                _blob.SetupGet(x => x.Metadata)
                    .Returns(new Dictionary<string, string>());
                _blob.Setup(x => x.SetMetadataAsync(It.IsNotNull<IAccessCondition>()))
                    .Returns(Task.FromResult(0));

                await _service.SetMetadataAsync(
                    folderName: CoreConstants.Folders.PackagesFolderName,
                    fileName: "a",
                    updateMetadataAsync: (lazyStream, metadata) =>
                    {
                        Assert.NotNull(metadata);

                        return Task.FromResult(true);
                    });

                _blob.VerifyAll();
                _blobContainer.VerifyAll();
                _blobClient.VerifyAll();
            }
        }

        public class TheSetPropertiesAsyncMethod
        {
            private const string _content = "peach";

            private readonly Mock<ICloudBlobClient> _blobClient;
            private readonly Mock<ICloudBlobContainer> _blobContainer;
            private readonly Mock<ISimpleCloudBlob> _blob;
            private readonly CloudBlobCoreFileStorageService _service;

            public TheSetPropertiesAsyncMethod()
            {
                _blobClient = new Mock<ICloudBlobClient>();
                _blobContainer = new Mock<ICloudBlobContainer>();
                _blob = new Mock<ISimpleCloudBlob>();

                _blobClient.Setup(x => x.GetContainerReference(It.IsAny<string>()))
                    .Returns(_blobContainer.Object);
                _blobContainer.Setup(x => x.CreateIfNotExistAsync(It.IsAny<bool>()))
                    .Returns(Task.FromResult(0));
                _blobContainer.Setup(x => x.GetBlobReference(It.IsAny<string>()))
                    .Returns(_blob.Object);

                _service = CreateService(fakeBlobClient: _blobClient);
            }

            [Fact]
            public async Task WhenLazyStreamRead_ReturnsContent()
            {
                _blob.Setup(x => x.DownloadToStreamAsync(It.IsAny<Stream>(), It.IsAny<IAccessCondition>()))
                    .Callback<Stream, IAccessCondition>((stream, _) =>
                    {
                        using (var writer = new StreamWriter(stream, Encoding.UTF8, bufferSize: 4096, leaveOpen: true))
                        {
                            writer.Write(_content);
                        }
                    })
                    .Returns(Task.FromResult(0));

                await _service.SetPropertiesAsync(
                    folderName: CoreConstants.Folders.PackagesFolderName,
                    fileName: "a",
                    updatePropertiesAsync: async (lazyStream, properties) =>
                    {
                        using (var stream = await lazyStream.Value)
                        using (var reader = new StreamReader(stream))
                        {
                            Assert.Equal(_content, reader.ReadToEnd());
                        }

                        return false;
                    });

                _blob.VerifyAll();
                _blobContainer.VerifyAll();
                _blobClient.VerifyAll();
            }

            [Fact]
            public async Task WhenReturnValueIsFalse_PropertyChangesAreNotPersisted()
            {
                _blob.SetupGet(x => x.Properties)
                    .Returns(Mock.Of<ICloudBlobProperties>());

                await _service.SetPropertiesAsync(
                    folderName: CoreConstants.Folders.PackagesFolderName,
                    fileName: "a",
                    updatePropertiesAsync: (lazyStream, properties) =>
                    {
                        Assert.NotNull(properties);

                        return Task.FromResult(false);
                    });

                _blob.VerifyAll();
                _blobContainer.VerifyAll();
                _blobClient.VerifyAll();
            }

            [Fact]
            public async Task WhenReturnValueIsTrue_PropertiesChangesArePersisted()
            {
                _blob.SetupGet(x => x.Properties)
                    .Returns(Mock.Of<ICloudBlobProperties>());
                _blob.Setup(x => x.SetPropertiesAsync(It.IsNotNull<IAccessCondition>()))
                    .Returns(Task.FromResult(0));

                await _service.SetPropertiesAsync(
                    folderName: CoreConstants.Folders.PackagesFolderName,
                    fileName: "a",
                    updatePropertiesAsync: (lazyStream, properties) =>
                    {
                        Assert.NotNull(properties);

                        return Task.FromResult(true);
                    });

                _blob.VerifyAll();
                _blobContainer.VerifyAll();
                _blobClient.VerifyAll();
            }
        }

        public class TheGetETagMethod
        {
            private const string _etag = "dummy_etag";

            private readonly Mock<ICloudBlobClient> _blobClient;
            private readonly Mock<ICloudBlobContainer> _blobContainer;
            private readonly Mock<ISimpleCloudBlob> _blob;
            private readonly CloudBlobCoreFileStorageService _service;

            public TheGetETagMethod()
            {
                _blobClient = new Mock<ICloudBlobClient>();
                _blobContainer = new Mock<ICloudBlobContainer>();
                _blob = new Mock<ISimpleCloudBlob>();

                _blobClient.Setup(x => x.GetContainerReference(It.IsAny<string>()))
                    .Returns(_blobContainer.Object);
                _blobContainer.Setup(x => x.CreateIfNotExistAsync(It.IsAny<bool>()))
                    .Returns(Task.FromResult(0));
                _blobContainer.Setup(x => x.GetBlobReference(It.IsAny<string>()))
                    .Returns(_blob.Object);

                _service = CreateService(fakeBlobClient: _blobClient);
            }

            [Fact]
            public async Task VerifyTheETagValue()
            {
                // Arrange
                _blob.SetupGet(x => x.ETag).Returns(_etag);

                // Act 
                var etagValue = await _service.GetETagOrNullAsync(folderName: CoreConstants.Folders.PackagesFolderName, fileName: "a");

                // Assert 
                Assert.Equal(_etag, etagValue);
            }


            [Fact]
            public async Task VerifyETagIsNullWhenBlobDoesNotExist()
            {
                // Arrange
                _blob.Setup(x => x.FetchAttributesAsync()).ThrowsAsync(new CloudBlobStorageException("Boo"));

                // Act 
                var etagValue = await _service.GetETagOrNullAsync(folderName: CoreConstants.Folders.PackagesFolderName, fileName: "a");

                // Assert 
                Assert.Null(etagValue);
            }
        }
    }
}