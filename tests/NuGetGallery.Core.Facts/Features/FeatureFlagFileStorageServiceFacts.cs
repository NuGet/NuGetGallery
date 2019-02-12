// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.WindowsAzure.Storage;
using Moq;
using Newtonsoft.Json;
using NuGet.Services.Entities;
using NuGet.Services.FeatureFlags;
using Xunit;

namespace NuGetGallery.Features
{
    public class FeatureFlagFileStorageServiceFacts
    {
        public class GetAsync : FactsBase
        {
            [Fact]
            public async Task DeserializesFlags()
            {
                // Arrange
                _storage
                    .Setup(s => s.GetFileAsync(CoreConstants.Folders.ContentFolderName, CoreConstants.FeatureFlagsFileName))
                    .ReturnsAsync(BuildStream(FeatureFlagJsonHelper.FormattedFullJson));

                // Act
                var result = await _target.GetAsync();

                // Assert
                Assert.Single(result.Features);
                Assert.True(result.Features.ContainsKey("NuGetGallery.Typosquatting"));
                Assert.Equal(FeatureStatus.Enabled, result.Features["NuGetGallery.Typosquatting"]);

                Assert.Single(result.Flights);
                Assert.True(result.Flights.ContainsKey("NuGetGallery.TyposquattingFlight"));
                Assert.True(result.Flights["NuGetGallery.TyposquattingFlight"].All);
                Assert.True(result.Flights["NuGetGallery.TyposquattingFlight"].SiteAdmins);
                Assert.Single(result.Flights["NuGetGallery.TyposquattingFlight"].Accounts, "a");
                Assert.Single(result.Flights["NuGetGallery.TyposquattingFlight"].Domains, "b");
            }

            [Fact]
            public async Task ThrowsOnInvalidJson()
            {
                _storage
                    .Setup(s => s.GetFileAsync(CoreConstants.Folders.ContentFolderName, CoreConstants.FeatureFlagsFileName))
                    .ReturnsAsync(BuildStream("Bad"));

                await Assert.ThrowsAsync<JsonReaderException>(() => _target.GetAsync());
            }
        }

        public class GetReferenceAsync : FactsBase
        {
            [Theory]
            [InlineData(FeatureFlagJsonHelper.UnformattedFullJson)]
            [InlineData(FeatureFlagJsonHelper.FormattedFullJson)]
            public async Task GetsAndFormatsFlags(string content)
            {
                // Arrange
                _storage
                    .Setup(s => s.GetFileReferenceAsync(CoreConstants.Folders.ContentFolderName, CoreConstants.FeatureFlagsFileName, null))
                    .ReturnsAsync(BuildFileReference(content, "bar"));

                // Act
                var result = await _target.GetReferenceAsync();

                // Assert - the flags should be formatted
                Assert.Equal(FeatureFlagJsonHelper.FormattedFullJson, result.FlagsJson);
                Assert.Equal("bar", result.ContentId);

                _storage
                    .Verify(
                        s => s.GetFileReferenceAsync(CoreConstants.Folders.ContentFolderName, CoreConstants.FeatureFlagsFileName, null),
                        Times.Once);
            }

            [Fact]
            public async Task ThrowsOnInvalidFlags()
            {
                // Arrange
                _storage
                    .Setup(s => s.GetFileReferenceAsync(CoreConstants.Folders.ContentFolderName, CoreConstants.FeatureFlagsFileName, null))
                    .ReturnsAsync(BuildFileReference("bad content", "bar"));

                // Act & Assert
                await Assert.ThrowsAsync<JsonReaderException>(() => _target.GetReferenceAsync());
            }
        }
    
        public class TrySaveAsync : FactsBase
        {
            [Theory]
            [MemberData(nameof(ReturnsOkData))]
            public async Task ReturnsOk(string content)
            {
                // Act
                var result = await _target.TrySaveAsync(content, "123");

                // Assert - the saved JSON should be formatted
                Assert.Equal(FeatureFlagSaveResult.Ok, result);

                _storage.Verify(
                    s => s.SaveFileAsync(
                        CoreConstants.Folders.ContentFolderName,
                        CoreConstants.FeatureFlagsFileName,
                        It.IsAny<Stream>(),
                        It.Is<IAccessCondition>(c => c.IfNoneMatchETag == null && c.IfMatchETag != null)),
                    Times.Once);
            }

            public static IEnumerable<object[]> ReturnsOkData()
            {
                foreach (var json in FeatureFlagJsonHelper.ValidJson)
                {
                    yield return new object[] { json };
                }
            }

            [Fact]
            public async Task FormatsSavedJson()
            {
                // Arrange
                string json = null;
                _storage.Setup(
                    s => s.SaveFileAsync(
                        CoreConstants.Folders.ContentFolderName,
                        CoreConstants.FeatureFlagsFileName,
                        It.IsAny<Stream>(),
                        It.Is<IAccessCondition>(c => c.IfNoneMatchETag == null && c.IfMatchETag != null)))
                    .Callback((string folder, string file, Stream content, IAccessCondition condition) =>
                    {
                        json = new StreamReader(content).ReadToEnd();
                    })
                    .Returns(Task.CompletedTask);

                // Act
                var result = await _target.TrySaveAsync(FeatureFlagJsonHelper.UnformattedFullJson, "123");

                // Assert - the saved JSON should be formatted
                Assert.Equal(FeatureFlagSaveResult.Ok, result);
                Assert.Equal(FeatureFlagJsonHelper.FormattedFullJson, json);

                _storage.Verify(
                    s => s.SaveFileAsync(
                        CoreConstants.Folders.ContentFolderName,
                        CoreConstants.FeatureFlagsFileName,
                        It.IsAny<Stream>(),
                        It.Is<IAccessCondition>(c => c.IfNoneMatchETag == null && c.IfMatchETag != null)),
                    Times.Once);
            }

            [Fact]
            public async Task IfStorageThrowsPreconditionFailedException_ReturnsConflict()
            {
                // Arrange
                _storage.Setup(
                    s => s.SaveFileAsync(
                        CoreConstants.Folders.ContentFolderName,
                        CoreConstants.FeatureFlagsFileName,
                        It.IsAny<Stream>(),
                        It.IsAny<IAccessCondition>()))
                    .ThrowsAsync(_preconditionException);

                // Act
                var result = await _target.TrySaveAsync(FeatureFlagJsonHelper.FormattedFullJson, "123");

                // Assert
                Assert.Equal(FeatureFlagSaveResult.Conflict, result);

                _storage.Verify(
                    s => s.SaveFileAsync(
                        CoreConstants.Folders.ContentFolderName,
                        CoreConstants.FeatureFlagsFileName,
                        It.IsAny<Stream>(),
                        It.Is<IAccessCondition>(c => c.IfNoneMatchETag == null && c.IfMatchETag != null)),
                    Times.Once);
            }

            [Theory]
            [MemberData(nameof(ReturnsInvalidData))]
            public async Task ReturnsInvalid(string badJson, string errorMessage)
            {
                // Act
                var result = await _target.TrySaveAsync(badJson, "123");

                // Assert
                Assert.Equal(FeatureFlagSaveResultType.Invalid, result.Type);
                Assert.Contains(errorMessage, result.Message);

                _storage.Verify(
                    s => s.SaveFileAsync(
                        It.IsAny<string>(),
                        It.IsAny<string>(),
                        It.IsAny<Stream>(),
                        It.IsAny<IAccessCondition>()),
                    Times.Never);
            }

            public static IEnumerable<object[]> ReturnsInvalidData()
            {
                yield return new object[]
                {
                    "bad",
                    "Unexpected character encountered while parsing value: b"
                };

                yield return new object[]
                {
                    "[]",
                    "Cannot deserialize the current JSON array (e.g. [1,2,3]) into type 'NuGet.Services.FeatureFlags.FeatureFlags'"
                };

                yield return new object[]
                {
                    @"{""Bad"": {}}",
                    "Could not find member 'Bad' on object of type 'FeatureFlags'"
                };

                yield return new object[]
                {
                    @"{""Features"": []}",
                    "Cannot deserialize the current JSON array (e.g. [1,2,3]) into type 'System.Collections.Generic.IReadOnlyDictionary`2[System.String,NuGet.Services.FeatureFlags.FeatureStatus]'"
                };

                yield return new object[]
                {
                    @"{""Features"": {""A"": ""bad""}}",
                    @"Error converting value ""bad"" to type 'NuGet.Services.FeatureFlags.FeatureStatus'"
                };

                yield return new object[]
                {
                    @"{""Flights"": []}",
                    "Cannot deserialize the current JSON array (e.g. [1,2,3]) into type 'System.Collections.Generic.IReadOnlyDictionary`2[System.String,NuGet.Services.FeatureFlags.Flight]'"
                };

                yield return new object[]
                {
                    @"{""Flights"": {""A"": []}}",
                    "Cannot deserialize the current JSON array (e.g. [1,2,3]) into type 'NuGet.Services.FeatureFlags.Flight'"
                };

                yield return new object[]
                {
                    @"{""Flights"": {""A"": {""bad"": 1}}}",
                    "Could not find member 'bad' on object of type 'Flight'"
                };

                yield return new object[]
                {
                    @"{""Flights"": {""A"": {""All"": ""bad""}}}",
                    "Could not convert string to boolean: bad"
                };
            }
        }

        public class TryRemoveUserAsync : FactsBase
        {
            [Fact]
            public async Task WhenUserNotInFlags_DoesntSave()
            {
                // Arrange
                var flags = new FeatureFlagBuilder()
                    .WithFlight("A", accounts: new List<string> { "user1", "user2" })
                    .Build();

                _storage
                    .Setup(s => s.GetFileReferenceAsync(CoreConstants.Folders.ContentFolderName, CoreConstants.FeatureFlagsFileName, null))
                    .ReturnsAsync(BuildFileReference(flags));

                // Act
                var result = await _target.TryRemoveUserAsync(new User { Username = "user3" });

                // Assert
                Assert.True(result);

                _storage.Verify(
                    s => s.SaveFileAsync(
                        It.IsAny<string>(),
                        It.IsAny<string>(),
                        It.IsAny<Stream>(),
                        It.IsAny<IAccessCondition>()),
                    Times.Never);
            }

            [Fact]
            public async Task RemovesUser()
            {
                string savedJson = null;
                var flags = new FeatureFlagBuilder()
                    .WithFlight("A", accounts: new List<string> { "user1", "user2" })
                    .WithFlight("B", accounts: new List<string> { "USER1" })
                    .WithFlight("C", accounts: new List<string> { "user2" })
                    .Build();

                _storage
                    .Setup(s => s.GetFileReferenceAsync(CoreConstants.Folders.ContentFolderName, CoreConstants.FeatureFlagsFileName, null))
                    .ReturnsAsync(BuildFileReference(flags));

                _storage
                    .Setup(s => s.SaveFileAsync(
                        CoreConstants.Folders.ContentFolderName,
                        CoreConstants.FeatureFlagsFileName,
                        It.IsAny<Stream>(),
                        It.IsAny<IAccessCondition>()))
                    .Callback((string folder, string file, Stream content, IAccessCondition condition) =>
                    {
                        savedJson = ToString(content);
                    })
                    .Returns(Task.CompletedTask);

                // Act
                var result = await _target.TryRemoveUserAsync(new User { Username = "user1" });

                // Arrange
                Assert.True(result);
                Assert.NotNull(savedJson);

                var savedFlags = JsonConvert.DeserializeObject<FeatureFlags>(savedJson);

                Assert.Equal(3, savedFlags.Flights.Count);
                Assert.Single(savedFlags.Flights["A"].Accounts);
                Assert.Empty(savedFlags.Flights["B"].Accounts);
                Assert.Single(savedFlags.Flights["C"].Accounts);

                Assert.Equal("user2", savedFlags.Flights["A"].Accounts[0]);
                Assert.Equal("user2", savedFlags.Flights["C"].Accounts[0]);

                _storage.Verify(
                    s => s.SaveFileAsync(
                        CoreConstants.Folders.ContentFolderName,
                        CoreConstants.FeatureFlagsFileName,
                        It.IsAny<Stream>(),
                        It.IsAny<IAccessCondition>()),
                    Times.Once);
            }

            [Fact]
            public async Task WhenSavePreconditionFailsOnce_Retries()
            {
                // Arrange
                var firstTry = true;
                string savedJson = null;
                var flags = new FeatureFlagBuilder()
                    .WithFlight("A", accounts: new List<string> { "user1", "user2" })
                    .Build();

                _storage
                    .Setup(s => s.GetFileReferenceAsync(CoreConstants.Folders.ContentFolderName, CoreConstants.FeatureFlagsFileName, null))
                    .ReturnsAsync(BuildFileReference(flags));

                _storage
                    .Setup(s => s.SaveFileAsync(
                        CoreConstants.Folders.ContentFolderName,
                        CoreConstants.FeatureFlagsFileName,
                        It.IsAny<Stream>(),
                        It.IsAny<IAccessCondition>()))
                    .Callback((string folder, string file, Stream content, IAccessCondition condition) =>
                    {
                        if (firstTry)
                        {
                            firstTry = false;
                            throw _preconditionException;
                        }

                        savedJson = ToString(content);
                    })
                    .Returns(Task.CompletedTask);

                // Act
                var result = await _target.TryRemoveUserAsync(new User { Username = "user1" });

                // Assert
                Assert.True(result);
                Assert.NotNull(savedJson);

                var savedFlags = JsonConvert.DeserializeObject<FeatureFlags>(savedJson);

                Assert.Single(savedFlags.Flights);
                Assert.Single(savedFlags.Flights["A"].Accounts);
                Assert.Equal("user2", savedFlags.Flights["A"].Accounts[0]);

                _storage.Verify(
                    s => s.SaveFileAsync(
                        CoreConstants.Folders.ContentFolderName,
                        CoreConstants.FeatureFlagsFileName,
                        It.IsAny<Stream>(),
                        It.IsAny<IAccessCondition>()),
                    Times.Exactly(2));
            }

            [Fact]
            public async Task WhenSavePreconditionAlwaysFails_ReturnsFalse()
            {
                // Arrange
                var flags = new FeatureFlagBuilder()
                    .WithFlight("A", accounts: new List<string> { "user1", "user2" })
                    .Build();

                _storage
                    .Setup(s => s.GetFileReferenceAsync(CoreConstants.Folders.ContentFolderName, CoreConstants.FeatureFlagsFileName, null))
                    .ReturnsAsync(BuildFileReference(flags));

                _storage
                    .Setup(s => s.SaveFileAsync(
                        CoreConstants.Folders.ContentFolderName,
                        CoreConstants.FeatureFlagsFileName,
                        It.IsAny<Stream>(),
                        It.IsAny<IAccessCondition>()))
                    .ThrowsAsync(_preconditionException);

                // Act
                var result = await _target.TryRemoveUserAsync(new User { Username = "user1" });

                // Assert
                Assert.False(result);

                _storage.Verify(
                    s => s.SaveFileAsync(
                        CoreConstants.Folders.ContentFolderName,
                        CoreConstants.FeatureFlagsFileName,
                        It.IsAny<Stream>(),
                        It.IsAny<IAccessCondition>()),
                    Times.Exactly(3));
            }

            [Fact]
            public async Task FormatsJson()
            {
                // Arrange
                string savedJson = null;

                _storage
                    .Setup(s => s.GetFileReferenceAsync(CoreConstants.Folders.ContentFolderName, CoreConstants.FeatureFlagsFileName, null))
                    .ReturnsAsync(BuildFileReference(@"{""Flights"": {""A"": {""Accounts"": [""user1"", ""user2""]}}}"));

                _storage
                    .Setup(s => s.SaveFileAsync(
                        CoreConstants.Folders.ContentFolderName,
                        CoreConstants.FeatureFlagsFileName,
                        It.IsAny<Stream>(),
                        It.IsAny<IAccessCondition>()))
                    .Callback((string folder, string file, Stream content, IAccessCondition condition) =>
                    {
                        savedJson = ToString(content);
                    })
                    .Returns(Task.CompletedTask);

                // Act
                var result = await _target.TryRemoveUserAsync(new User { Username = "user1" });

                // Assert
                Assert.True(result);
                Assert.NotNull(savedJson);

                var expectedJson = @"{
  ""Features"": {},
  ""Flights"": {
    ""A"": {
      ""All"": false,
      ""SiteAdmins"": false,
      ""Accounts"": [
        ""user2""
      ],
      ""Domains"": []
    }
  }
}";

                Assert.Equal(expectedJson, savedJson);

                _storage.Verify(
                    s => s.SaveFileAsync(
                        CoreConstants.Folders.ContentFolderName,
                        CoreConstants.FeatureFlagsFileName,
                        It.IsAny<Stream>(),
                        It.IsAny<IAccessCondition>()),
                    Times.Once);
            }

            private class FeatureFlagBuilder
            {
                private readonly Dictionary<string, Flight> _flights = new Dictionary<string, Flight>();

                public FeatureFlagBuilder WithFlight(string name, IReadOnlyList<string> accounts)
                {
                    _flights[name] = new Flight(all: false, siteAdmins: false, accounts: accounts, domains: new List<string>());

                    return this;
                }

                public FeatureFlags Build()
                {
                    return new FeatureFlags(
                        new Dictionary<string, FeatureStatus>(),
                        _flights);
                }
            }
        }

        public class FactsBase
        {
            protected readonly Mock<ICoreFileStorageService> _storage;
            protected readonly FeatureFlagFileStorageService _target;
            protected readonly StorageException _preconditionException;

            public FactsBase()
            {
                var logger = Mock.Of<ILogger<FeatureFlagFileStorageService>>();

                _storage = new Mock<ICoreFileStorageService>();
                _target = new FeatureFlagFileStorageService(_storage.Object, logger);

                _preconditionException = new StorageException(
                    new RequestResult
                    {
                        HttpStatusCode = (int)HttpStatusCode.PreconditionFailed
                    },
                    "Precondition failed",
                    new Exception());
            }

            protected Stream BuildStream(string content)
            {
                return new MemoryStream(Encoding.UTF8.GetBytes(content ?? ""));
            }

            protected string ToString(Stream content)
            {
                using (var reader = new StreamReader(content))
                {
                    return reader.ReadToEnd();
                }
            }

            protected IFileReference BuildFileReference(string content, string contentId = null)
            {
                return new FileReference
                {
                    Stream = BuildStream(content),
                    ContentId = contentId ?? "fake-content-id",
                };
            }

            protected IFileReference BuildFileReference(FeatureFlags content, string contentId = null)
            {
                return BuildFileReference(
                    JsonConvert.SerializeObject(content),
                    contentId ?? "fake-content-id");
            }

            private class FileReference : IFileReference
            {
                public Stream Stream { get; set; }
                public string ContentId { get; set; }

                public Stream OpenRead()
                {
                    var copy = new MemoryStream();

                    Stream.Position = 0;
                    Stream.CopyTo(copy);
                    copy.Position = 0;

                    return copy;
                }
            }
        }
    }
}
