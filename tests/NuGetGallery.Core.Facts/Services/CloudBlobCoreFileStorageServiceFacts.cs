// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Reflection;
using System.Threading.Tasks;
using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Blob;
using Microsoft.WindowsAzure.Storage.Blob.Protocol;
using Moq;
using Xunit;
using Xunit.Sdk;

namespace NuGetGallery
{
    public class CloudBlobCoreFileStorageServiceFacts
    {
        private static CloudBlobCoreFileStorageService CreateService(
            Mock<ICloudBlobClient> fakeBlobClient = null)
        {
            if (fakeBlobClient == null)
            {
                fakeBlobClient = new Mock<ICloudBlobClient>();
            }

            return new CloudBlobCoreFileStorageService(fakeBlobClient.Object);
        }

        private class FolderNamesDataAttribute : DataAttribute
        {
            public FolderNamesDataAttribute(bool includePermissions = false, bool includeContentTypes = false)
            {
                IncludePermissions = includePermissions;
                IncludeContentTypes = includeContentTypes;
            }

            private bool IncludePermissions { get; }

            private bool IncludeContentTypes { get; }

            public override IEnumerable<object[]> GetData(MethodInfo testMethod)
            {
                var folderNames = new List<object[]>
                {
                    new object[] { CoreConstants.ContentFolderName, false, null, },
                    new object[] { CoreConstants.DownloadsFolderName, true, CoreConstants.OctetStreamContentType },
                    new object[] { CoreConstants.PackageBackupsFolderName, true, CoreConstants.PackageContentType },
                    new object[] { CoreConstants.PackageReadMesFolderName, false, CoreConstants.TextContentType },
                    new object[] { CoreConstants.PackagesFolderName, true, CoreConstants.PackageContentType },
                    new object[] { CoreConstants.UploadsFolderName, false, CoreConstants.PackageContentType },
                    new object[] { CoreConstants.ValidationFolderName, false, CoreConstants.PackageContentType },
                };

                if (!IncludePermissions && !IncludeContentTypes)
                {
                    folderNames = folderNames
                        .Select(fn => new[] { fn.ElementAt(0) })
                        .ToList();
                }
                else if (IncludePermissions && !IncludeContentTypes)
                {
                    folderNames = folderNames
                        .Select(fn => new[] { fn[0], fn[1] })
                        .ToList();
                }
                else if (!IncludePermissions && IncludeContentTypes)
                {
                    folderNames = folderNames
                        .Where(fn => fn[2] != null)
                        .Select(fn => new[] { fn[0], fn[2] })
                        .ToList();
                }

                return folderNames;
            }
        }
        
        public class TheCtor
        {
            [Theory]
            [FolderNamesData]
            public async Task WillCreateABlobContainerForDemandedFoldersIfTheyDoNotExist(string folderName)
            {
                var fakeBlobClient = new Mock<ICloudBlobClient>();
                var fakeBlobContainer = new Mock<ICloudBlobContainer>();
                fakeBlobContainer.Setup(x => x.CreateIfNotExistAsync()).Returns(Task.FromResult(0)).Verifiable();
                fakeBlobContainer.Setup(x => x.SetPermissionsAsync(It.IsAny<BlobContainerPermissions>())).Returns(Task.FromResult(0));
                var simpleCloudBlob = new Mock<ISimpleCloudBlob>();
                simpleCloudBlob.Setup(x => x.DownloadToStreamAsync(It.IsAny<Stream>(), It.IsAny<AccessCondition>())).Returns(Task.FromResult(0));
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
                fakeBlobContainer.Setup(x => x.SetPermissionsAsync(It.IsAny<BlobContainerPermissions>()))
                    .Returns(Task.FromResult(0))
                    .Verifiable();

                var simpleCloudBlob = new Mock<ISimpleCloudBlob>();
                simpleCloudBlob.Setup(x => x.DownloadToStreamAsync(It.IsAny<Stream>(), It.IsAny<AccessCondition>())).Returns(Task.FromResult(0));

                fakeBlobContainer.Setup(x => x.CreateIfNotExistAsync()).Returns(Task.FromResult(0));
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
                                blobContainer.Setup(x => x.CreateIfNotExistAsync()).Returns(Task.FromResult(0));
                                blobContainer.Setup(x => x.SetPermissionsAsync(It.IsAny<BlobContainerPermissions>())).Returns(Task.FromResult(0));
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
                fakeBlobContainer.Setup(x => x.CreateIfNotExistAsync()).Returns(Task.FromResult(0));
                fakeBlobContainer.Setup(x => x.SetPermissionsAsync(It.IsAny<BlobContainerPermissions>())).Returns(Task.FromResult(0));
                fakeBlob.Setup(x => x.Uri).Returns(new Uri("http://theUri"));
                var service = CreateService(fakeBlobClient: fakeBlobClient);

                await service.DeleteFileAsync(CoreConstants.PackagesFolderName, "theFileName");

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

                                containerMock.Setup(x => x.CreateIfNotExistAsync()).Returns(Task.FromResult(0));
                                containerMock.Setup(x => x.SetPermissionsAsync(It.IsAny<BlobContainerPermissions>())).Returns(Task.FromResult(0));
                                return containerMock.Object;
                            });
                fakeBlobContainer.Setup(x => x.GetBlobReference(It.IsAny<string>())).Returns(fakeBlob.Object);
                fakeBlob.Setup(x => x.DownloadToStreamAsync(It.IsAny<Stream>(), It.IsAny<AccessCondition>())).Returns(Task.FromResult(0)).Verifiable();
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
                                blobContainer.Setup(x => x.CreateIfNotExistAsync()).Returns(Task.FromResult(0));
                                blobContainer.Setup(x => x.SetPermissionsAsync(It.IsAny<BlobContainerPermissions>())).Returns(Task.FromResult(0));
                                return blobContainer.Object;
                            });
                fakeBlobContainer.Setup(x => x.GetBlobReference(It.IsAny<string>())).Returns(fakeBlob.Object);
                fakeBlob.Setup(x => x.DownloadToStreamAsync(It.IsAny<Stream>(), It.IsAny<AccessCondition>()))
                    .Callback<Stream, AccessCondition>((x, _) => { x.WriteByte(42); })
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
                                blobContainer.Setup(x => x.CreateIfNotExistAsync()).Returns(Task.FromResult(0));
                                blobContainer.Setup(x => x.SetPermissionsAsync(It.IsAny<BlobContainerPermissions>())).Returns(Task.FromResult(0));
                                return blobContainer.Object;
                            });
                fakeBlobContainer.Setup(x => x.GetBlobReference(It.IsAny<string>())).Returns(fakeBlob.Object);

                fakeBlob.Setup(x => x.DownloadToStreamAsync(It.IsAny<Stream>(), It.IsAny<AccessCondition>())).Throws(
                    new TestableStorageClientException { ErrorCode = BlobErrorCodeStrings.BlobNotFound });
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
                                blobContainer.Setup(x => x.CreateIfNotExistAsync()).Returns(Task.FromResult(0));
                                blobContainer.Setup(x => x.SetPermissionsAsync(It.IsAny<BlobContainerPermissions>())).Returns(Task.FromResult(0));
                                return blobContainer.Object;
                            });
                fakeBlobContainer.Setup(x => x.GetBlobReference(It.IsAny<string>())).Returns(fakeBlob.Object);
                fakeBlob.Setup(x => x.DownloadToStreamAsync(It.IsAny<Stream>(), It.IsAny<AccessCondition>()))
                        .Callback<Stream, AccessCondition>((x, _) => { x.WriteByte(42); })
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
                                blobContainer.Setup(x => x.CreateIfNotExistAsync()).Returns(Task.FromResult(0));
                                blobContainer.Setup(x => x.SetPermissionsAsync(It.IsAny<BlobContainerPermissions>())).Returns(Task.FromResult(0));
                                return blobContainer.Object;
                            });
                fakeBlobContainer.Setup(x => x.GetBlobReference(It.IsAny<string>())).Returns(fakeBlob.Object);
                fakeBlob.Setup(x => x.Properties).Returns(new BlobProperties());
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
                fakeBlobContainer.Setup(x => x.SetPermissionsAsync(It.IsAny<BlobContainerPermissions>())).Returns(Task.FromResult(0));
                fakeBlobContainer.Setup(x => x.CreateIfNotExistAsync()).Returns(Task.FromResult(0));
                fakeBlob.Setup(x => x.Properties).Returns(new BlobProperties());
                fakeBlob.Setup(x => x.Uri).Returns(new Uri("http://theUri"));
                var service = CreateService(fakeBlobClient: fakeBlobClient);

                await service.SaveFileAsync(CoreConstants.PackagesFolderName, "theFileName", new MemoryStream());

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
                    .Throws(new StorageException(
                        new RequestResult { HttpStatusCode = (int)HttpStatusCode.Conflict },
                        "Conflict!",
                        new Exception("inner")));
                fakeBlobClient.Setup(x => x.GetContainerReference(It.IsAny<string>())).Returns(fakeBlobContainer.Object);
                fakeBlobContainer.Setup(x => x.GetBlobReference(It.IsAny<string>())).Returns(fakeBlob.Object);
                fakeBlobContainer.Setup(x => x.SetPermissionsAsync(It.IsAny<BlobContainerPermissions>())).Returns(Task.FromResult(0));
                fakeBlobContainer.Setup(x => x.CreateIfNotExistAsync()).Returns(Task.FromResult(0));
                fakeBlob.Setup(x => x.Properties).Returns(new BlobProperties());
                fakeBlob.Setup(x => x.Uri).Returns(new Uri("http://theUri"));
                var service = CreateService(fakeBlobClient: fakeBlobClient);

                await Assert.ThrowsAsync<InvalidOperationException>(async () => await service.SaveFileAsync(CoreConstants.PackagesFolderName, "theFileName", new MemoryStream(), overwrite: false));

                fakeBlob.Verify();
            }

            [Fact]
            public async Task WillUploadThePackageFileToTheBlob()
            {
                var fakeBlobClient = new Mock<ICloudBlobClient>();
                var fakeBlobContainer = new Mock<ICloudBlobContainer>();
                fakeBlobContainer.Setup(x => x.CreateIfNotExistAsync()).Returns(Task.FromResult(0));
                fakeBlobContainer.Setup(x => x.SetPermissionsAsync(It.IsAny<BlobContainerPermissions>())).Returns(Task.FromResult(0));
                var fakeBlob = new Mock<ISimpleCloudBlob>();
                fakeBlobClient.Setup(x => x.GetContainerReference(It.IsAny<string>())).Returns(fakeBlobContainer.Object);
                fakeBlobContainer.Setup(x => x.GetBlobReference(It.IsAny<string>())).Returns(fakeBlob.Object);
                fakeBlob.Setup(x => x.Properties).Returns(new BlobProperties());
                fakeBlob.Setup(x => x.Uri).Returns(new Uri("http://theUri"));
                fakeBlob.Setup(x => x.DeleteIfExistsAsync()).Returns(Task.FromResult(0));
                fakeBlob.Setup(x => x.SetPropertiesAsync()).Returns(Task.FromResult(0));
                var service = CreateService(fakeBlobClient: fakeBlobClient);
                var fakePackageFile = new MemoryStream();
                fakeBlob.Setup(x => x.UploadFromStreamAsync(fakePackageFile, true)).Returns(Task.FromResult(0)).Verifiable();

                await service.SaveFileAsync(CoreConstants.PackagesFolderName, "theFileName", fakePackageFile);

                fakeBlob.Verify();
            }

            [Theory]
            [FolderNamesData(includeContentTypes: true)]
            public async Task WillSetTheBlobContentType(string folderName, string contentType)
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
                                blobContainer.Setup(x => x.CreateIfNotExistAsync()).Returns(Task.FromResult(0));
                                blobContainer.Setup(x => x.SetPermissionsAsync(It.IsAny<BlobContainerPermissions>())).Returns(Task.FromResult(0));
                                return blobContainer.Object;
                            });
                fakeBlobContainer.Setup(x => x.GetBlobReference(It.IsAny<string>())).Returns(fakeBlob.Object);
                fakeBlob.Setup(x => x.Properties).Returns(new BlobProperties());
                fakeBlob.Setup(x => x.Uri).Returns(new Uri("http://theUri"));
                fakeBlob.Setup(x => x.DeleteIfExistsAsync()).Returns(Task.FromResult(0));
                fakeBlob.Setup(x => x.UploadFromStreamAsync(It.IsAny<Stream>(), true)).Returns(Task.FromResult(0));
                fakeBlob.Setup(x => x.SetPropertiesAsync()).Returns(Task.FromResult(0));
                var service = CreateService(fakeBlobClient: fakeBlobClient);

                await service.SaveFileAsync(folderName, "theFileName", new MemoryStream());

                Assert.Equal(contentType, fakeBlob.Object.Properties.ContentType);
                fakeBlob.Verify(x => x.SetPropertiesAsync());
            }
        }

        public class TheGetFileReadUriAsyncMethod
        {
            [Fact]
            public async Task WillThrowIfFolderIsNull()
            {
                var service = CreateService();

                var ex = await Assert.ThrowsAsync<ArgumentNullException>(() => service.GetFileReadUriAsync(null, "theFileName", DateTimeOffset.UtcNow.AddHours(3)));
                Assert.Equal("folderName", ex.ParamName);
            }

            [Fact]
            public async Task WillThrowIfFilenameIsNull()
            {
                var service = CreateService();

                var ex = await Assert.ThrowsAsync<ArgumentNullException>(() => service.GetFileReadUriAsync("theFolder", null, DateTimeOffset.UtcNow.AddHours(3)));
                Assert.Equal("fileName", ex.ParamName);
            }

            [Fact]
            public async Task WillThrowIfEndOfAccessIsInThePast()
            {
                var service = CreateService();

                DateTimeOffset inThePast = DateTimeOffset.UtcNow.AddSeconds(-1);
                var ex = await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => service.GetFileReadUriAsync("theFolder", "theFileName", inThePast));
                Assert.Equal("endOfAccess", ex.ParamName);
            }
        }
    }
}
