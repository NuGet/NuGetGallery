// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Xml.Linq;
using Moq;
using NuGetGallery.DataProtection;
using NuGetGallery.Diagnostics;
using Xunit;

namespace NuGetGallery
{
    public class FileStorageXmlRepositoryFacts
    {
        [Fact]
        public void GetAllElementsReadsEveryXmlFileInOrdinalOrder()
        {
            var files = new Dictionary<string, string>
            {
                ["z.xml"] = "<key id=\"z\" />",
                ["nested/a.xml"] = "<key id=\"a\" />",
                ["ignored.txt"] = "not xml",
            };
            FileStorageXmlRepository target = CreateRepository(files);

            IReadOnlyCollection<XElement> result = target.GetAllElements();

            Assert.Equal(new[] { "a", "z" }, result.Select(element => (string)element.Attribute("id")));
        }

        [Fact]
        public void GetAllElementsRejectsMalformedXml()
        {
            FileStorageXmlRepository target = CreateRepository(
                new Dictionary<string, string> { ["bad.xml"] = "<key>" });

            InvalidOperationException exception = Assert.Throws<InvalidOperationException>(
                () => target.GetAllElements());

            Assert.Equal("A Data Protection key-ring file contains malformed XML.", exception.Message);
        }

        [Fact]
        public void GetAllElementsRejectsDocumentTypeDefinitions()
        {
            FileStorageXmlRepository target = CreateRepository(
                new Dictionary<string, string>
                {
                    ["bad.xml"] = "<!DOCTYPE key [<!ENTITY value \"secret\">]><key>&value;</key>",
                });

            Assert.Throws<InvalidOperationException>(() => target.GetAllElements());
        }

        [Fact]
        public void GetAllElementsRetriesAFileBeingCompletedByAnotherWriter()
        {
            int readAttempts = 0;
            var storage = new Mock<ICoreFileStorageService>();
            storage
                .Setup(x => x.ListFilesAsync(CoreConstants.Folders.DataProtectionFolderName))
                .ReturnsAsync(new[] { "key.xml" });
            storage
                .Setup(x => x.GetFileAsync(
                    CoreConstants.Folders.DataProtectionFolderName,
                    "key.xml"))
                .Returns(() =>
                {
                    readAttempts++;
                    if (readAttempts == 1)
                    {
                        throw new IOException("The writer still has the file locked.");
                    }

                    return Task.FromResult<Stream>(
                        new MemoryStream(Encoding.UTF8.GetBytes("<key id=\"ready\" />")));
                });
            var target = new FileStorageXmlRepository(storage.Object, Mock.Of<IDiagnosticsService>());

            XElement result = Assert.Single(target.GetAllElements());

            Assert.Equal("ready", (string)result.Attribute("id"));
            Assert.Equal(2, readAttempts);
        }

        [Fact]
        public void StoreElementUsesAnImmutableXmlWrite()
        {
            bool? overwrite = null;
            string folderName = null;
            string fileName = null;
            string contentType = null;
            string content = null;
            var storage = new Mock<ICoreFileStorageService>();
            storage
                .Setup(x => x.SaveFileAsync(
                    It.IsAny<string>(),
                    It.IsAny<string>(),
                    It.IsAny<string>(),
                    It.IsAny<Stream>(),
                    It.IsAny<bool>()))
                .Returns<string, string, string, Stream, bool>((folder, file, type, stream, shouldOverwrite) =>
                {
                    folderName = folder;
                    fileName = file;
                    contentType = type;
                    overwrite = shouldOverwrite;
                    using (var reader = new StreamReader(stream, Encoding.UTF8, true, 1024, leaveOpen: true))
                    {
                        content = reader.ReadToEnd();
                    }

                    return Task.CompletedTask;
                });
            var target = new FileStorageXmlRepository(storage.Object, Mock.Of<IDiagnosticsService>());

            target.StoreElement(new XElement("key", new XAttribute("id", "one")), "key-one");

            Assert.Equal(CoreConstants.Folders.DataProtectionFolderName, folderName);
            Assert.Equal("key-one.xml", fileName);
            Assert.Equal(CoreConstants.XmlContentType, contentType);
            Assert.False(overwrite);
            Assert.Contains("id=\"one\"", content);
        }

        [Fact]
        public async Task ConcurrentCreateOnlyWritesAllowExactlyOneWinner()
        {
            var files = new ConcurrentDictionary<string, byte[]>();
            var storage = new Mock<ICoreFileStorageService>();
            storage
                .Setup(x => x.SaveFileAsync(
                    CoreConstants.Folders.DataProtectionFolderName,
                    It.IsAny<string>(),
                    CoreConstants.XmlContentType,
                    It.IsAny<Stream>(),
                    false))
                .Returns<string, string, string, Stream, bool>((_, fileName, __, stream, ___) =>
                {
                    using (var copy = new MemoryStream())
                    {
                        stream.CopyTo(copy);
                        if (!files.TryAdd(fileName, copy.ToArray()))
                        {
                            throw new FileAlreadyExistsException("The immutable key already exists.");
                        }
                    }

                    return Task.CompletedTask;
                });
            var target = new FileStorageXmlRepository(storage.Object, Mock.Of<IDiagnosticsService>());

            Task<bool>[] writes = Enumerable.Range(0, 16)
                .Select(_ => Task.Run(() =>
                {
                    try
                    {
                        target.StoreElement(new XElement("key"), "same-key");
                        return true;
                    }
                    catch (FileAlreadyExistsException)
                    {
                        return false;
                    }
                }))
                .ToArray();

            bool[] results = await Task.WhenAll(writes);

            Assert.Single(results.Where(result => result));
            Assert.Single(files);
        }

        [Fact]
        public void SynchronousRepositoryDoesNotDependOnTheRequestSynchronizationContext()
        {
            var storage = new Mock<ICoreFileStorageService>();
            storage
                .Setup(x => x.ListFilesAsync(CoreConstants.Folders.DataProtectionFolderName))
                .Returns(async () =>
                {
                    await Task.Yield();
                    return (IReadOnlyList<string>)Array.Empty<string>();
                });
            var target = new FileStorageXmlRepository(storage.Object, Mock.Of<IDiagnosticsService>());
            SynchronizationContext original = SynchronizationContext.Current;
            SynchronizationContext.SetSynchronizationContext(new NonPumpingSynchronizationContext());

            try
            {
                Assert.Empty(target.GetAllElements());
            }
            finally
            {
                SynchronizationContext.SetSynchronizationContext(original);
            }
        }

        [Fact]
        public void KeyGenerationDoesNotDependOnTheRequestSynchronizationContext()
        {
            bool stored = false;
            var storage = new Mock<ICoreFileStorageService>();
            storage
                .Setup(x => x.SaveFileAsync(
                    CoreConstants.Folders.DataProtectionFolderName,
                    "request-key.xml",
                    CoreConstants.XmlContentType,
                    It.IsAny<Stream>(),
                    false))
                .Returns(async () =>
                {
                    await Task.Yield();
                    stored = true;
                });
            var target = new FileStorageXmlRepository(storage.Object, Mock.Of<IDiagnosticsService>());
            SynchronizationContext original = SynchronizationContext.Current;
            SynchronizationContext.SetSynchronizationContext(new NonPumpingSynchronizationContext());

            try
            {
                target.StoreElement(new XElement("key"), "request-key");
            }
            finally
            {
                SynchronizationContext.SetSynchronizationContext(original);
            }

            Assert.True(stored);
        }

        private static FileStorageXmlRepository CreateRepository(IReadOnlyDictionary<string, string> files)
        {
            var storage = new Mock<ICoreFileStorageService>();
            storage
                .Setup(x => x.ListFilesAsync(CoreConstants.Folders.DataProtectionFolderName))
                .ReturnsAsync(files.Keys.ToList());
            storage
                .Setup(x => x.GetFileAsync(CoreConstants.Folders.DataProtectionFolderName, It.IsAny<string>()))
                .ReturnsAsync((string _, string fileName) =>
                    files.TryGetValue(fileName, out string content)
                        ? new MemoryStream(Encoding.UTF8.GetBytes(content))
                        : null);

            return new FileStorageXmlRepository(storage.Object, Mock.Of<IDiagnosticsService>());
        }

        private sealed class NonPumpingSynchronizationContext : SynchronizationContext
        {
            public override void Post(SendOrPostCallback callback, object state)
            {
            }
        }
    }
}
