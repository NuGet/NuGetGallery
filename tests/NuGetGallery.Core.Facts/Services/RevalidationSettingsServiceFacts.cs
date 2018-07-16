// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.IO;
using System.Net;
using System.Threading.Tasks;
using Microsoft.WindowsAzure.Storage;
using Moq;
using Newtonsoft.Json;
using Xunit;

namespace NuGetGallery.Services
{
    public class RevalidationSettingsServiceFacts
    {
        public class TheGetSettingsAsyncMethod : FactsBase
        {
            [Fact]
            public async Task ThrowsIfFileDoesNotExist()
            {
                _storage
                    .Setup(s => s.GetFileReferenceAsync(CoreConstants.RevalidationFolderName, "settings.json", null))
                    .ReturnsAsync((string folder, string file, bool ifNoneMatch) => null);

                var e = await Assert.ThrowsAsync<InvalidOperationException>(() => _target.GetSettingsAsync());

                Assert.Equal("Could not find file 'settings.json' in folder 'revalidation'", e.Message);
            }

            [Fact]
            public async Task ThrowsIfFileIsMalformed()
            {
                using (Mock(""))
                {
                    var e = await Assert.ThrowsAsync<InvalidOperationException>(() => _target.GetSettingsAsync());

                    Assert.Equal("Settings blob 'settings.json' in folder 'revalidation' is malformed", e.Message);
                }
            }

            [Fact]
            public async Task GetsLatestSettings()
            {
                using (Mock("{'Initialized': true, 'Killswitch': true, 'DesiredPackageEventRate': 123}"))
                {
                    var settings = await _target.GetSettingsAsync();

                    Assert.True(settings.Initialized);
                    Assert.True(settings.Killswitch);
                    Assert.Equal(123, settings.DesiredPackageEventRate);
                }
            }

            [Fact]
            public async Task InitializedDefaultsToFalse()
            {
                using (Mock("{'Killswitch': true, 'DesiredPackageEventRate': 123}"))
                {
                    var settings = await _target.GetSettingsAsync();

                    Assert.False(settings.Initialized);
                    Assert.True(settings.Killswitch);
                    Assert.Equal(123, settings.DesiredPackageEventRate);
                }
            }

            [Fact]
            public async Task KillswitchDefaultsToFalse()
            {
                using (Mock("{'Initialized': true, 'DesiredPackageEventRate': 123}"))
                {
                    var settings = await _target.GetSettingsAsync();

                    Assert.True(settings.Initialized);
                    Assert.False(settings.Killswitch);
                    Assert.Equal(123, settings.DesiredPackageEventRate);
                }
            }

            [Fact]
            public async Task DesiredPackageEventRateDefaultsToZero()
            {
                using (Mock("{'Initialized': true, 'Killswitch': true}"))
                {
                    var settings = await _target.GetSettingsAsync();

                    Assert.True(settings.Initialized);
                    Assert.True(settings.Killswitch);
                    Assert.Equal(0, settings.DesiredPackageEventRate);
                }
            }
        }

        public class TheUpdateSettingsAsyncMethod : FactsBase
        {
            [Fact]
            public async Task UpdatesLatestSettings()
            {
                using (Mock("{'Initialized': false, 'Killswitch': true, 'DesiredPackageEventRate': 123}", contentId: "foo-bar"))
                {
                    await _target.UpdateSettingsAsync(settings =>
                    {
                        Assert.False(settings.Initialized);
                        Assert.True(settings.Killswitch);
                        Assert.Equal(123, settings.DesiredPackageEventRate);

                        settings.Initialized = true;
                        settings.Killswitch = false;
                        settings.DesiredPackageEventRate = 456;
                    });
                }

                _storage.Verify(
                    s => s.SaveFileAsync(
                        "revalidation",
                        "settings.json",
                        It.IsAny<Stream>(),
                        It.Is<IAccessCondition>(c => c.IfNoneMatchETag == null && c.IfMatchETag == "foo-bar")),
                    Times.Once);

                Assert.NotNull(_lastUploadedSettings);

                var settingsResult = JsonConvert.DeserializeObject<RevalidationSettings>(_lastUploadedSettings);

                Assert.True(settingsResult.Initialized);
                Assert.False(settingsResult.Killswitch);
                Assert.Equal(456, settingsResult.DesiredPackageEventRate);
            }

            [Fact]
            public async Task ThrowsOnConcurrencyIssue()
            {
                using (Mock(storageExceptionCode: HttpStatusCode.PreconditionFailed))
                {
                    var e = await Assert.ThrowsAsync<InvalidOperationException>(() => _target.UpdateSettingsAsync(settings => { }));

                    Assert.Equal("Failed to update the settings blob as the access condition failed", e.Message);
                }
            }
        }

        public class TheMaybeUpdateSettingsAsyncMethod : FactsBase
        {
            [Fact]
            public async Task DoesntUpdateSettingsIfReturnsFalse()
            {
                using (Mock("{'Initialized': false, 'Killswitch': true, 'DesiredPackageEventRate': 123}", contentId: "foo-bar"))
                {
                    await _target.MaybeUpdateSettingsAsync(settings =>
                    {
                        Assert.False(settings.Initialized);
                        Assert.True(settings.Killswitch);
                        Assert.Equal(123, settings.DesiredPackageEventRate);

                        settings.Initialized = true;
                        settings.Killswitch = false;
                        settings.DesiredPackageEventRate = 456;

                        return false;
                    });
                }

                _storage.Verify(s => s.SaveFileAsync("revalidation", "settings.json", It.IsAny<Stream>(), It.IsAny<IAccessCondition>()), Times.Never);

                Assert.Null(_lastUploadedSettings);
            }

            [Fact]
            public async Task UpdateSettingsIfReturnsTrue()
            {
                using (Mock("{'Initialized': false, 'Killswitch': true, 'DesiredPackageEventRate': 123}", contentId: "foo-bar"))
                {
                    await _target.MaybeUpdateSettingsAsync(settings =>
                    {
                        Assert.False(settings.Initialized);
                        Assert.True(settings.Killswitch);
                        Assert.Equal(123, settings.DesiredPackageEventRate);

                        settings.Initialized = true;
                        settings.Killswitch = false;
                        settings.DesiredPackageEventRate = 456;

                        return true;
                    });
                }

                _storage.Verify(
                    s => s.SaveFileAsync(
                        "revalidation",
                        "settings.json",
                        It.IsAny<Stream>(),
                        It.Is<IAccessCondition>(c => c.IfNoneMatchETag == null && c.IfMatchETag == "foo-bar")),
                    Times.Once);

                Assert.NotNull(_lastUploadedSettings);

                var settingsResult = JsonConvert.DeserializeObject<RevalidationSettings>(_lastUploadedSettings);

                Assert.True(settingsResult.Initialized);
                Assert.False(settingsResult.Killswitch);
                Assert.Equal(456, settingsResult.DesiredPackageEventRate);
            }

            [Fact]
            public async Task ThrowsOnConcurrencyIssue()
            {
                using (Mock(storageExceptionCode: HttpStatusCode.PreconditionFailed))
                {
                    var e = await Assert.ThrowsAsync<InvalidOperationException>(() => _target.MaybeUpdateSettingsAsync(settings => true));

                    Assert.Equal("Failed to update the settings blob as the access condition failed", e.Message);
                }
            }
        }

        public class FactsBase
        {
            protected readonly Mock<ICoreFileStorageService> _storage;
            protected readonly RevalidationSettingsService _target;

            protected string _lastUploadedSettings = null;

            public FactsBase()
            {
                _storage = new Mock<ICoreFileStorageService>();

                _target = new RevalidationSettingsService(_storage.Object);
            }

            protected IDisposable Mock(
                string settings = null,
                string contentId = null,
                HttpStatusCode? storageExceptionCode = null)
            {
                var stream = new MemoryStream();
                var writer = new StreamWriter(stream);
                writer.Write(settings ?? "{'Initialized': false, 'Killswitch': true, 'DesiredPackageEventRate': 123}");
                writer.Flush();
                stream.Position = 0;

                var fileReference = new TestableFileReference(stream, writer, contentId);

                _storage
                    .Setup(s => s.GetFileReferenceAsync(CoreConstants.RevalidationFolderName, "settings.json", null))
                    .ReturnsAsync((string folder, string file, bool ifNoneMatch) => fileReference);

                if (storageExceptionCode.HasValue)
                {
                    var concurrencyResult = new RequestResult
                    {
                        HttpStatusCode = (int)HttpStatusCode.PreconditionFailed
                    };

                    var concurrencyException = new StorageException(concurrencyResult, "Concurrency exception", inner: null);

                    _storage
                        .Setup(s => s.SaveFileAsync("revalidation", "settings.json", It.IsAny<Stream>(), It.IsAny<IAccessCondition>()))
                        .ThrowsAsync(concurrencyException);
                }
                else
                {
                    _storage
                        .Setup(s => s.SaveFileAsync("revalidation", "settings.json", It.IsAny<Stream>(), It.IsAny<IAccessCondition>()))
                        .Callback((string folder, string file, Stream uploadedStream, IAccessCondition condition) =>
                        {
                            using (var reader = new StreamReader(uploadedStream))
                            {
                                _lastUploadedSettings = reader.ReadToEnd();
                            }
                        })
                        .Returns(Task.CompletedTask);
                }

                return fileReference;
            }

            protected RevalidationSettings Deserialize(string input)
            {
                using (var reader = new StreamReader(input))
                using (var jsonReader = new JsonTextReader(reader))
                {
                    return new JsonSerializer().Deserialize<RevalidationSettings>(jsonReader);
                }
            }

            private class TestableFileReference : IFileReference, IDisposable
            {
                private readonly Stream _stream;
                private readonly StreamWriter _writer;

                public TestableFileReference(Stream stream, StreamWriter writer, string contentId)
                {
                    _stream = stream;
                    _writer = writer;
                    ContentId = contentId;
                }

                public string ContentId { get; }

                public Stream OpenRead() => _stream;

                public void Dispose()
                {
                    _stream?.Dispose();
                    _writer?.Dispose();
                }
            }
        }
    }
}
