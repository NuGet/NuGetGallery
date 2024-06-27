// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.IO;
using System.Net;
using System.Threading.Tasks;
using Moq;
using Newtonsoft.Json;
using Xunit;

namespace NuGetGallery.Services
{
    public class RevalidationStateServiceFacts
    {
        public class TheGetStateAsyncMethod : FactsBase
        {
            [Fact]
            public async Task ThrowsIfFileDoesNotExist()
            {
                _storage
                    .Setup(s => s.GetFileReferenceAsync(CoreConstants.Folders.RevalidationFolderName, "state.json", null))
                    .ReturnsAsync((string folder, string file, bool ifNoneMatch) => null);

                var e = await Assert.ThrowsAsync<InvalidOperationException>(() => _target.GetStateAsync());

                Assert.Equal("Could not find file 'state.json' in folder 'revalidation'", e.Message);
            }

            [Fact]
            public async Task ThrowsIfFileIsMalformed()
            {
                using (Mock(""))
                {
                    var e = await Assert.ThrowsAsync<InvalidOperationException>(() => _target.GetStateAsync());

                    Assert.Equal("State blob 'state.json' in folder 'revalidation' is malformed", e.Message);
                }
            }

            [Fact]
            public async Task GetsLatestState()
            {
                using (Mock("{'IsInitialized': true, 'IsKillswitchActive': true, 'DesiredPackageEventRate': 123}"))
                {
                    var state = await _target.GetStateAsync();

                    Assert.True(state.IsInitialized);
                    Assert.True(state.IsKillswitchActive);
                    Assert.Equal(123, state.DesiredPackageEventRate);
                }
            }

            [Fact]
            public async Task InitializedDefaultsToFalse()
            {
                using (Mock("{'IsKillswitchActive': true, 'DesiredPackageEventRate': 123}"))
                {
                    var state = await _target.GetStateAsync();

                    Assert.False(state.IsInitialized);
                    Assert.True(state.IsKillswitchActive);
                    Assert.Equal(123, state.DesiredPackageEventRate);
                }
            }

            [Fact]
            public async Task KillswitchDefaultsToFalse()
            {
                using (Mock("{'IsInitialized': true, 'DesiredPackageEventRate': 123}"))
                {
                    var state = await _target.GetStateAsync();

                    Assert.True(state.IsInitialized);
                    Assert.False(state.IsKillswitchActive);
                    Assert.Equal(123, state.DesiredPackageEventRate);
                }
            }

            [Fact]
            public async Task DesiredPackageEventRateDefaultsToZero()
            {
                using (Mock("{'IsInitialized': true, 'IsKillswitchActive': true}"))
                {
                    var state = await _target.GetStateAsync();

                    Assert.True(state.IsInitialized);
                    Assert.True(state.IsKillswitchActive);
                    Assert.Equal(0, state.DesiredPackageEventRate);
                }
            }
        }

        public class TheUpdateStateAsyncMethod : FactsBase
        {
            [Fact]
            public async Task UpdatesLatestState()
            {
                using (Mock("{'IsInitialized': false, 'IsKillswitchActive': true, 'DesiredPackageEventRate': 123}", contentId: "foo-bar"))
                {
                    await _target.UpdateStateAsync(state =>
                    {
                        Assert.False(state.IsInitialized);
                        Assert.True(state.IsKillswitchActive);
                        Assert.Equal(123, state.DesiredPackageEventRate);

                        state.IsInitialized = true;
                        state.IsKillswitchActive = false;
                        state.DesiredPackageEventRate = 456;
                    });
                }

                _storage.Verify(
                    s => s.SaveFileAsync(
                        "revalidation",
                        "state.json",
                        It.IsAny<Stream>(),
                        It.Is<IAccessCondition>(c => c.IfNoneMatchETag == null && c.IfMatchETag == "foo-bar")),
                    Times.Once);

                Assert.NotNull(_lastUploadedState);

                var stateResult = JsonConvert.DeserializeObject<RevalidationState>(_lastUploadedState);

                Assert.True(stateResult.IsInitialized);
                Assert.False(stateResult.IsKillswitchActive);
                Assert.Equal(456, stateResult.DesiredPackageEventRate);
            }

            [Fact]
            public async Task ThrowsOnConcurrencyIssue()
            {
                using (Mock(storageExceptionCode: HttpStatusCode.PreconditionFailed))
                {
                    var e = await Assert.ThrowsAsync<InvalidOperationException>(() => _target.UpdateStateAsync(state => { }));

                    Assert.Equal("Failed to update the state blob since the access condition failed", e.Message);
                }
            }
        }

        public class TheMaybeUpdateStateAsyncMethod : FactsBase
        {
            [Fact]
            public async Task DoesntUpdateStateIfReturnsFalse()
            {
                RevalidationState result;

                using (Mock("{'IsInitialized': false, 'IsKillswitchActive': true, 'DesiredPackageEventRate': 123}", contentId: "foo-bar"))
                {
                    result = await _target.MaybeUpdateStateAsync(state =>
                    {
                        Assert.False(state.IsInitialized);
                        Assert.True(state.IsKillswitchActive);
                        Assert.Equal(123, state.DesiredPackageEventRate);

                        state.IsInitialized = true;
                        state.IsKillswitchActive = false;
                        state.DesiredPackageEventRate = 456;

                        return false;
                    });
                }

                _storage.Verify(s => s.SaveFileAsync("revalidation", "state.json", It.IsAny<Stream>(), It.IsAny<IAccessCondition>()), Times.Never);

                Assert.Null(_lastUploadedState);
                Assert.False(result.IsInitialized);
                Assert.True(result.IsKillswitchActive);
                Assert.Equal(123, result.DesiredPackageEventRate);
            }

            [Fact]
            public async Task UpdatesStateIfReturnsTrue()
            {
                using (Mock("{'IsInitialized': false, 'IsKillswitchActive': true, 'DesiredPackageEventRate': 123}", contentId: "foo-bar"))
                {
                    await _target.MaybeUpdateStateAsync(state =>
                    {
                        Assert.False(state.IsInitialized);
                        Assert.True(state.IsKillswitchActive);
                        Assert.Equal(123, state.DesiredPackageEventRate);

                        state.IsInitialized = true;
                        state.IsKillswitchActive = false;
                        state.DesiredPackageEventRate = 456;

                        return true;
                    });
                }

                _storage.Verify(
                    s => s.SaveFileAsync(
                        "revalidation",
                        "state.json",
                        It.IsAny<Stream>(),
                        It.Is<IAccessCondition>(c => c.IfNoneMatchETag == null && c.IfMatchETag == "foo-bar")),
                    Times.Once);

                Assert.NotNull(_lastUploadedState);

                var stateResult = JsonConvert.DeserializeObject<RevalidationState>(_lastUploadedState);

                Assert.True(stateResult.IsInitialized);
                Assert.False(stateResult.IsKillswitchActive);
                Assert.Equal(456, stateResult.DesiredPackageEventRate);
            }

            [Fact]
            public async Task ThrowsOnConcurrencyIssue()
            {
                using (Mock(storageExceptionCode: HttpStatusCode.PreconditionFailed))
                {
                    var e = await Assert.ThrowsAsync<InvalidOperationException>(() => _target.MaybeUpdateStateAsync(state => true));

                    Assert.Equal("Failed to update the state blob since the access condition failed", e.Message);
                }
            }
        }

        public class FactsBase
        {
            protected readonly Mock<ICoreFileStorageService> _storage;
            protected readonly RevalidationStateService _target;

            protected string _lastUploadedState = null;

            public FactsBase()
            {
                _storage = new Mock<ICoreFileStorageService>();

                _target = new RevalidationStateService(_storage.Object);
            }

            protected IDisposable Mock(
                string state = null,
                string contentId = null,
                HttpStatusCode? storageExceptionCode = null)
            {
                var stream = new MemoryStream();
                var writer = new StreamWriter(stream);
                writer.Write(state ?? "{'IsInitialized': false, 'IsKillswitchActive': true, 'DesiredPackageEventRate': 123}");
                writer.Flush();
                stream.Position = 0;

                var fileReference = new TestableFileReference(stream, writer, contentId);

                _storage
                    .Setup(s => s.GetFileReferenceAsync(CoreConstants.Folders.RevalidationFolderName, "state.json", null))
                    .ReturnsAsync((string folder, string file, bool ifNoneMatch) => fileReference);

                if (storageExceptionCode.HasValue)
                {
                    _storage
                        .Setup(s => s.SaveFileAsync("revalidation", "state.json", It.IsAny<Stream>(), It.IsAny<IAccessCondition>()))
                        .ThrowsAsync(new CloudBlobPreconditionFailedException(null));
                }
                else
                {
                    _storage
                        .Setup(s => s.SaveFileAsync("revalidation", "state.json", It.IsAny<Stream>(), It.IsAny<IAccessCondition>()))
                        .Callback((string folder, string file, Stream uploadedStream, IAccessCondition condition) =>
                        {
                            using (var reader = new StreamReader(uploadedStream))
                            {
                                _lastUploadedState = reader.ReadToEnd();
                            }
                        })
                        .Returns(Task.CompletedTask);
                }

                return fileReference;
            }

            protected RevalidationState Deserialize(string input)
            {
                using (var reader = new StreamReader(input))
                using (var jsonReader = new JsonTextReader(reader))
                {
                    return new JsonSerializer().Deserialize<RevalidationState>(jsonReader);
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
