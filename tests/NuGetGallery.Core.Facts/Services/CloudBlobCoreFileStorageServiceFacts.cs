﻿// Copyright (c) .NET Foundation. All rights reserved.
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
using NuGetGallery.Diagnostics;
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

            return new CloudBlobCoreFileStorageService(fakeBlobClient.Object, Mock.Of<IDiagnosticsService>());
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

                await Assert.ThrowsAsync<FileAlreadyExistsException>(async () => await service.SaveFileAsync(CoreConstants.PackagesFolderName, "theFileName", new MemoryStream(), overwrite: false));

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

        public class TheGetPriviledgedFileUriAsyncMethod
        {
            private const string folderName = "theFolderName";
            private const string fileName = "theFileName";
            private const string signature = "?secret=42";

            [Fact]
            public async Task WillThrowIfFolderIsNull()
            {
                var service = CreateService();

                var ex = await Assert.ThrowsAsync<ArgumentNullException>(() => service.GetPriviledgedFileUriAsync(
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

                var ex = await Assert.ThrowsAsync<ArgumentNullException>(() => service.GetPriviledgedFileUriAsync(
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
                var ex = await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => service.GetPriviledgedFileUriAsync(
                    folderName,
                    fileName,
                    FileUriPermissions.Read,
                    inThePast));
                Assert.Equal("endOfAccess", ex.ParamName);
            }

            [Theory]
            [InlineData(CoreConstants.ValidationFolderName, "http://example.com/" + CoreConstants.ValidationFolderName + "/" + fileName + signature)]
            [InlineData(CoreConstants.PackagesFolderName, "http://example.com/" + CoreConstants.PackagesFolderName + "/" + fileName + signature)]
            public async Task WillAlwaysUseSasTokenDependingOnContainerAvailability(string containerName, string expectedUri)
            {
                var setupResult = Setup(containerName, fileName);
                var fakeBlobClient = setupResult.Item1;
                var fakeBlob = setupResult.Item2;
                var blobUri = setupResult.Item3;

                fakeBlob
                    .Setup(b => b.GetSharedAccessSignature(SharedAccessBlobPermissions.Read, It.IsAny<DateTimeOffset?>()))
                    .Returns(signature);
                var service = CreateService(fakeBlobClient);

                var uri = await service.GetPriviledgedFileUriAsync(
                    containerName,
                    fileName,
                    FileUriPermissions.Read,
                    DateTimeOffset.Now.AddHours(3));

                Assert.Equal(expectedUri, uri.AbsoluteUri);
            }

            [Fact]
            public async Task WillPassTheEndOfAccessTimestampFurther()
            {
                const string folderName = CoreConstants.ValidationFolderName;
                const string fileName = "theFileName";
                const string signature = "?secret=42";
                DateTimeOffset endOfAccess = DateTimeOffset.Now.AddHours(3);
                var setupResult = Setup(folderName, fileName);
                var fakeBlobClient = setupResult.Item1;
                var fakeBlob = setupResult.Item2;
                var blobUri = setupResult.Item3;

                fakeBlob
                    .Setup(b => b.GetSharedAccessSignature(
                        SharedAccessBlobPermissions.Read | SharedAccessBlobPermissions.Delete,
                        endOfAccess))
                    .Returns(signature)
                    .Verifiable();

                var service = CreateService(fakeBlobClient);

                var uri = await service.GetPriviledgedFileUriAsync(
                    folderName,
                    fileName,
                    FileUriPermissions.Read | FileUriPermissions.Delete,
                    endOfAccess);

                string expectedUri = new Uri(blobUri, signature).AbsoluteUri;
                Assert.Equal(expectedUri, uri.AbsoluteUri);
                fakeBlob.Verify(
                    b => b.GetSharedAccessSignature(SharedAccessBlobPermissions.Read | SharedAccessBlobPermissions.Delete, endOfAccess),
                    Times.Once);
                fakeBlob.Verify(
                    b => b.GetSharedAccessSignature(It.IsAny<SharedAccessBlobPermissions>(),
                    It.IsAny<DateTimeOffset?>()), Times.Once);
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
            public async Task WillThrowIfEndOfAccessIsInThePast()
            {
                var service = CreateService();

                DateTimeOffset inThePast = DateTimeOffset.UtcNow.AddSeconds(-1);
                var ex = await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => service.GetFileReadUriAsync(folderName, fileName, inThePast));
                Assert.Equal("endOfAccess", ex.ParamName);
            }

            [Theory]
            [InlineData(CoreConstants.ValidationFolderName, "http://example.com/" + CoreConstants.ValidationFolderName + "/" + fileName + signature)]
            [InlineData(CoreConstants.PackagesFolderName, "http://example.com/" + CoreConstants.PackagesFolderName + "/" + fileName)]
            public async Task WillUseSasTokenDependingOnContainerAvailability(string containerName, string expectedUri)
            {
                var setupResult = Setup(containerName, fileName);
                var fakeBlobClient = setupResult.Item1;
                var fakeBlob = setupResult.Item2;
                var blobUri = setupResult.Item3;

                fakeBlob
                    .Setup(b => b.GetSharedAccessSignature(SharedAccessBlobPermissions.Read, It.IsAny<DateTimeOffset?>()))
                    .Returns(signature);
                var service = CreateService(fakeBlobClient);

                var uri = await service.GetFileReadUriAsync(containerName, fileName, DateTimeOffset.Now.AddHours(3));

                Assert.Equal(expectedUri, uri.AbsoluteUri);
            }

            [Fact]
            public async Task WillThrowIfNoEndOfAccessSpecifiedForNonPublicContainer()
            {
                var service = CreateService();

                var ex = await Assert.ThrowsAsync<ArgumentNullException>(() => service.GetFileReadUriAsync(CoreConstants.ValidationFolderName, fileName, null));
                Assert.Equal("endOfAccess", ex.ParamName);
            }

            [Fact]
            public async Task WillNotThrowIfNoEndOfAccessSpecifiedForPublicContainer()
            {
                const string packagesFolderName = CoreConstants.PackagesFolderName;
                var setupResult = Setup(packagesFolderName, fileName);
                var service = CreateService(setupResult.Item1);

                var ex = await Record.ExceptionAsync(() => service.GetFileReadUriAsync(packagesFolderName, fileName, null));

                Assert.Null(ex);
            }

            [Fact]
            public async Task WillPassTheEndOfAccessTimestampFurther()
            {
                const string folderName = CoreConstants.ValidationFolderName;
                const string signature = "?secret=42";
                DateTimeOffset endOfAccess = DateTimeOffset.Now.AddHours(3);
                var setupResult = Setup(folderName, fileName);
                var fakeBlobClient = setupResult.Item1;
                var fakeBlob = setupResult.Item2;
                var blobUri = setupResult.Item3;

                fakeBlob
                    .Setup(b => b.GetSharedAccessSignature(SharedAccessBlobPermissions.Read, endOfAccess))
                    .Returns(signature)
                    .Verifiable();

                var service = CreateService(fakeBlobClient);

                var uri = await service.GetFileReadUriAsync(folderName, fileName, endOfAccess);

                string expectedUri = new Uri(blobUri, signature).AbsoluteUri;
                Assert.Equal(expectedUri, uri.AbsoluteUri);
                fakeBlob.Verify(b => b.GetSharedAccessSignature(SharedAccessBlobPermissions.Read, endOfAccess), Times.Once);
                fakeBlob.Verify(b => b.GetSharedAccessSignature(It.IsAny<SharedAccessBlobPermissions>(), It.IsAny<DateTimeOffset?>()), Times.Once);
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
            private BlobProperties _srcProperties;
            private string _destFolderName;
            private string _destFileName;
            private string _destETag;
            private BlobProperties _destProperties;
            private CopyState _destCopyState;
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
                _srcUri = new Uri("https://example/nuget.versioning.4.5.0.nupkg");
                _srcProperties = new BlobProperties();
                _destFolderName = "packages";
                _destFileName = "nuget.versioning.4.5.0.nupkg";
                _destETag = "\"dest-etag\"";
                _destProperties = new BlobProperties();
                _destCopyState = new CopyState();
                SetDestCopyStatus(CopyStatus.Success);

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
                    .Returns(() => _srcProperties);
                _destBlobMock
                    .Setup(x => x.ETag)
                    .Returns(() => _destETag);
                _destBlobMock
                    .Setup(x => x.Properties)
                    .Returns(() => _destProperties);
                _destBlobMock
                    .Setup(x => x.CopyState)
                    .Returns(() => _destCopyState);

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
                    .Setup(x => x.StartCopyAsync(It.IsAny<ISimpleCloudBlob>(), It.IsAny<AccessCondition>(), It.IsAny<AccessCondition>()))
                    .Returns(Task.FromResult(0))
                    .Callback<ISimpleCloudBlob, AccessCondition, AccessCondition>((_, __, ___) =>
                    {
                        SetDestCopyStatus(CopyStatus.Success);
                    });

                // Act
                await _target.CopyFileAsync(
                    _srcUri,
                    _destFolderName,
                    _destFileName,
                    AccessConditionWrapper.GenerateIfNotExistsCondition());

                // Assert
                _destBlobMock.Verify(
                    x => x.StartCopyAsync(_srcBlobMock.Object, It.IsAny<AccessCondition>(), It.IsAny<AccessCondition>()),
                    Times.Once);
                _destBlobMock.Verify(
                    x => x.StartCopyAsync(It.IsAny<ISimpleCloudBlob>(), It.IsAny<AccessCondition>(), It.IsAny<AccessCondition>()),
                    Times.Once);
                _blobClient.Verify(
                    x => x.GetBlobFromUri(_srcUri),
                    Times.Once);
            }

            [Fact]
            public async Task WillCopyTheFileIfDestinationDoesNotExist()
            {
                // Arrange
                AccessCondition srcAccessCondition = null;
                AccessCondition destAccessCondition = null;
                ISimpleCloudBlob srcBlob = null;

                _destBlobMock
                    .Setup(x => x.StartCopyAsync(It.IsAny<ISimpleCloudBlob>(), It.IsAny<AccessCondition>(), It.IsAny<AccessCondition>()))
                    .Returns(Task.FromResult(0))
                    .Callback<ISimpleCloudBlob, AccessCondition, AccessCondition>((b, s, d) =>
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
                    x => x.StartCopyAsync(It.IsAny<ISimpleCloudBlob>(), It.IsAny<AccessCondition>(), It.IsAny<AccessCondition>()),
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
                    .Setup(x => x.StartCopyAsync(It.IsAny<ISimpleCloudBlob>(), It.IsAny<AccessCondition>(), It.IsAny<AccessCondition>()))
                    .Throws(new StorageException(new RequestResult { HttpStatusCode = (int)HttpStatusCode.Conflict }, "Conflict!", inner: null));

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
                AccessCondition srcAccessCondition = null;
                AccessCondition destAccessCondition = null;
                ISimpleCloudBlob srcBlob = null;

                SetDestCopyStatus(CopyStatus.Failed);

                _destBlobMock
                    .Setup(x => x.StartCopyAsync(It.IsAny<ISimpleCloudBlob>(), It.IsAny<AccessCondition>(), It.IsAny<AccessCondition>()))
                    .Returns(Task.FromResult(0))
                    .Callback<ISimpleCloudBlob, AccessCondition, AccessCondition>((b, s, d) =>
                    {
                        srcBlob = b;
                        srcAccessCondition = s;
                        destAccessCondition = d;
                        SetDestCopyStatus(CopyStatus.Pending);
                    });

                _destBlobMock
                    .Setup(x => x.ExistsAsync())
                    .ReturnsAsync(true);

                _destBlobMock
                    .Setup(x => x.FetchAttributesAsync())
                    .Returns(Task.FromResult(0))
                    .Callback(() => SetDestCopyStatus(CopyStatus.Success));

                // Act
                var srcETag = await _target.CopyFileAsync(
                    _srcFolderName,
                    _srcFileName,
                    _destFolderName,
                    _destFileName,
                    AccessConditionWrapper.GenerateIfNotExistsCondition());

                // Assert
                _destBlobMock.Verify(
                    x => x.StartCopyAsync(It.IsAny<ISimpleCloudBlob>(), It.IsAny<AccessCondition>(), It.IsAny<AccessCondition>()),
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
                AccessCondition srcAccessCondition = null;
                AccessCondition destAccessCondition = null;
                ISimpleCloudBlob srcBlob = null;
                _destBlobMock
                    .Setup(x => x.StartCopyAsync(It.IsAny<ISimpleCloudBlob>(), It.IsAny<AccessCondition>(), It.IsAny<AccessCondition>()))
                    .Returns(Task.FromResult(0))
                    .Callback<ISimpleCloudBlob, AccessCondition, AccessCondition>((b, s, d) =>
                    {
                        srcBlob = b;
                        srcAccessCondition = s;
                        destAccessCondition = d;
                        SetDestCopyStatus(CopyStatus.Success);
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
                    x => x.StartCopyAsync(It.IsAny<ISimpleCloudBlob>(), It.IsAny<AccessCondition>(), It.IsAny<AccessCondition>()),
                    Times.Once);
                Assert.Null(destAccessCondition.IfMatchETag);
                Assert.Equal("*", destAccessCondition.IfNoneMatchETag);
            }

            [Fact]
            public async Task UsesProvidedMatchETag()
            {
                // Arrange
                AccessCondition srcAccessCondition = null;
                AccessCondition destAccessCondition = null;
                ISimpleCloudBlob srcBlob = null;
                _destBlobMock
                    .Setup(x => x.StartCopyAsync(It.IsAny<ISimpleCloudBlob>(), It.IsAny<AccessCondition>(), It.IsAny<AccessCondition>()))
                    .Returns(Task.FromResult(0))
                    .Callback<ISimpleCloudBlob, AccessCondition, AccessCondition>((b, s, d) =>
                    {
                        srcBlob = b;
                        srcAccessCondition = s;
                        destAccessCondition = d;
                        SetDestCopyStatus(CopyStatus.Success);
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
                    x => x.StartCopyAsync(It.IsAny<ISimpleCloudBlob>(), It.IsAny<AccessCondition>(), It.IsAny<AccessCondition>()),
                    Times.Once);
                Assert.Equal("etag!", destAccessCondition.IfMatchETag);
                Assert.Null(destAccessCondition.IfNoneMatchETag);
            }

            [Fact]
            public async Task NoOpsIfPackageLengthAndHashMatch()
            {
                // Arrange
                SetBlobContentMD5(_srcProperties, "mwgwUC0MwohHxgMmvQzO7A==");
                SetBlobLength(_srcProperties, 42);
                SetBlobContentMD5(_destProperties, _srcProperties.ContentMD5);
                SetBlobLength(_destProperties, _srcProperties.Length);

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
                    x => x.StartCopyAsync(It.IsAny<ISimpleCloudBlob>(), It.IsAny<AccessCondition>(), It.IsAny<AccessCondition>()),
                    Times.Never);
            }

            [Fact]
            public async Task ThrowsIfCopyOperationFails()
            {
                // Arrange
                _destBlobMock
                    .Setup(x => x.StartCopyAsync(It.IsAny<ISimpleCloudBlob>(), It.IsAny<AccessCondition>(), It.IsAny<AccessCondition>()))
                    .Returns(Task.FromResult(0))
                    .Callback<ISimpleCloudBlob, AccessCondition, AccessCondition>((_, __, ___) =>
                    {
                        SetDestCopyStatus(CopyStatus.Failed);
                    });

                // Act & Assert
                var ex = await Assert.ThrowsAsync<StorageException>(
                    () => _target.CopyFileAsync(
                        _srcFolderName,
                        _srcFileName,
                        _destFolderName,
                        _destFileName,
                        AccessConditionWrapper.GenerateIfNotExistsCondition()));
                Assert.Contains("The blob copy operation had copy status Failed", ex.Message);
            }

            private void SetDestCopyStatus(CopyStatus copyStatus)
            {
                // We have to use reflection because the setter is not public.
                typeof(CopyState)
                    .GetProperty(nameof(CopyState.Status))
                    .SetValue(_destCopyState, copyStatus, null);
            }

            private void SetBlobLength(BlobProperties properties, long length)
            {
                typeof(BlobProperties)
                    .GetProperty(nameof(BlobProperties.Length))
                    .SetValue(properties, length, null);
            }

            private void SetBlobContentMD5(BlobProperties properties, string contentMD5)
            {
                typeof(BlobProperties)
                    .GetProperty(nameof(BlobProperties.ContentMD5))
                    .SetValue(properties, contentMD5, null);
            }
        }
    }
}
