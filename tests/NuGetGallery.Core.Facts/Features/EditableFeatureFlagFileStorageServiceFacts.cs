// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Moq;
using Newtonsoft.Json;
using NuGet.Services.Entities;
using NuGet.Services.FeatureFlags;
using NuGetGallery.Auditing;
using NuGetGallery.Shared;
using Xunit;

namespace NuGetGallery.Features
{
    public class EditableFeatureFlagFileStorageServiceFacts
    {
        public static FeatureFlags Example = new FeatureFlags(
            new Dictionary<string, FeatureStatus>
            {
                {
                    "NuGetGallery.Typosquatting",
                    FeatureStatus.Enabled
                }
            },
            new Dictionary<string, Flight>
            {
                {
                    "NuGetGallery.TyposquattingFlight",
                    new Flight(true, true, new [] { "a" }, new[] { "b" })
                }
            });

        public const string ExampleJson = @"{
  ""Features"": {
    ""NuGetGallery.Typosquatting"": ""Enabled""
  },
  ""Flights"": {
    ""NuGetGallery.TyposquattingFlight"": {
      ""All"": true,
      ""SiteAdmins"": true,
      ""Accounts"": [
        ""a""
      ],
      ""Domains"": [
        ""b""
      ]
    }
  }
}";

        public static void AssertExample(FeatureFlags actual)
        {
            Assert.Single(actual.Features);
            Assert.True(actual.Features.ContainsKey("NuGetGallery.Typosquatting"));
            Assert.Equal(FeatureStatus.Enabled, actual.Features["NuGetGallery.Typosquatting"]);

            Assert.Single(actual.Flights);
            Assert.True(actual.Flights.ContainsKey("NuGetGallery.TyposquattingFlight"));
            Assert.True(actual.Flights["NuGetGallery.TyposquattingFlight"].All);
            Assert.True(actual.Flights["NuGetGallery.TyposquattingFlight"].SiteAdmins);
            Assert.Single(actual.Flights["NuGetGallery.TyposquattingFlight"].Accounts, "a");
            Assert.Single(actual.Flights["NuGetGallery.TyposquattingFlight"].Domains, "b");
        }

        public class GetAsync : FactsBase
        {
            [Fact]
            public async Task DeserializesFlags()
            {
                // Arrange
                _storage
                    .Setup(s => s.GetFileAsync(CoreConstants.Folders.ContentFolderName, CoreConstants.FeatureFlagsFileName))
                    .ReturnsAsync(BuildStream(ExampleJson));

                // Act
                var result = await _target.GetAsync();

                // Assert
                AssertExample(result);
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
            [Fact]
            public async Task GetsAndFormatsFlags()
            {
                // Arrange
                _storage
                    .Setup(s => s.GetFileReferenceAsync(CoreConstants.Folders.ContentFolderName, CoreConstants.FeatureFlagsFileName, null))
                    .ReturnsAsync(BuildFileReference(ExampleJson, "bar"));

                // Act
                var result = await _target.GetReferenceAsync();

                // Assert - the flags should be formatted
                AssertExample(result.Flags);
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
            [Fact]
            public async Task ReturnsOk()
            {
                var contentId = "123";

                // Act
                var result = await _target.TrySaveAsync(Example, contentId);

                // Assert - the saved JSON should be formatted
                Assert.Equal(ContentSaveResult.Ok, result);

                _storage.Verify(
                    s => s.SaveFileAsync(
                        CoreConstants.Folders.ContentFolderName,
                        CoreConstants.FeatureFlagsFileName,
                        It.IsAny<Stream>(),
                        It.Is<IAccessCondition>(c => c.IfNoneMatchETag == null && c.IfMatchETag != null)),
                    Times.Once);

                _auditing.Verify(
                    a => a.SaveAuditRecordAsync(
                        It.Is<FeatureFlagsAuditRecord>(
                            r => r.Action == AuditedFeatureFlagsAction.Update
                                && r.ContentId == contentId
                                && r.Result == ContentSaveResult.Ok
                                && r.Features.SingleOrDefault(
                                    f => f.Name == "NuGetGallery.Typosquatting"
                                        && f.Status == FeatureStatus.Enabled) != null
                                && r.Flights.SingleOrDefault(
                                    f => f.Name == "NuGetGallery.TyposquattingFlight"
                                        && f.All
                                        && f.SiteAdmins
                                        && f.Accounts.Single() == "a"
                                        && f.Domains.Single() == "b") != null)),
                    Times.Once());
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

                var contentId = "123";

                // Act
                var result = await _target.TrySaveAsync(Example, contentId);

                // Assert - the saved JSON should be formatted
                Assert.Equal(ContentSaveResult.Ok, result);
                Assert.Equal(ExampleJson, json);

                _storage.Verify(
                    s => s.SaveFileAsync(
                        CoreConstants.Folders.ContentFolderName,
                        CoreConstants.FeatureFlagsFileName,
                        It.IsAny<Stream>(),
                        It.Is<IAccessCondition>(c => c.IfNoneMatchETag == null && c.IfMatchETag != null)),
                    Times.Once);

                _auditing.Verify(
                    a => a.SaveAuditRecordAsync(
                        It.Is<FeatureFlagsAuditRecord>(
                            r => r.Action == AuditedFeatureFlagsAction.Update
                                && r.ContentId == contentId
                                && r.Result == ContentSaveResult.Ok
                                && r.Features.SingleOrDefault(
                                    f => f.Name == "NuGetGallery.Typosquatting"
                                        && f.Status == FeatureStatus.Enabled) != null
                                && r.Flights.SingleOrDefault(
                                    f => f.Name == "NuGetGallery.TyposquattingFlight"
                                        && f.All
                                        && f.SiteAdmins
                                        && f.Accounts.Single() == "a"
                                        && f.Domains.Single() == "b") != null)),
                    Times.Once());
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

                var contentId = "123";

                // Act
                var result = await _target.TrySaveAsync(Example, contentId);

                // Assert
                Assert.Equal(ContentSaveResult.Conflict, result);

                _storage.Verify(
                    s => s.SaveFileAsync(
                        CoreConstants.Folders.ContentFolderName,
                        CoreConstants.FeatureFlagsFileName,
                        It.IsAny<Stream>(),
                        It.Is<IAccessCondition>(c => c.IfNoneMatchETag == null && c.IfMatchETag != null)),
                    Times.Once);

                _auditing.Verify(
                    a => a.SaveAuditRecordAsync(
                        It.Is<FeatureFlagsAuditRecord>(
                            r => r.Action == AuditedFeatureFlagsAction.Update
                                && r.ContentId == contentId
                                && r.Result == ContentSaveResult.Conflict
                                && r.Features.SingleOrDefault(
                                    f => f.Name == "NuGetGallery.Typosquatting"
                                        && f.Status == FeatureStatus.Enabled) != null
                                && r.Flights.SingleOrDefault(
                                    f => f.Name == "NuGetGallery.TyposquattingFlight"
                                        && f.All
                                        && f.SiteAdmins
                                        && f.Accounts.Single() == "a"
                                        && f.Domains.Single() == "b") != null)),
                    Times.Once());
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
                await _target.RemoveUserAsync(new User { Username = "user3" });

                // Assert
                _storage.Verify(
                    s => s.SaveFileAsync(
                        It.IsAny<string>(),
                        It.IsAny<string>(),
                        It.IsAny<Stream>(),
                        It.IsAny<IAccessCondition>()),
                    Times.Never);

                _auditing.Verify(
                    a => a.SaveAuditRecordAsync(
                        It.IsAny<FeatureFlagsAuditRecord>()),
                    Times.Never());
            }

            [Fact]
            public async Task WhenExtraDataInJson_Throws()
            {
                // Arrange
                _storage
                    .Setup(s => s.GetFileReferenceAsync(CoreConstants.Folders.ContentFolderName, CoreConstants.FeatureFlagsFileName, null))
                    .ReturnsAsync(BuildFileReference(@"{""Invalid"": true}"));

                // Act
                var exception = await Assert.ThrowsAsync<JsonSerializationException>(() => _target.RemoveUserAsync(new User { Username = "user1" }));

                // Assert
                Assert.Contains("Could not find member 'Invalid' on object of type 'FeatureFlags'", exception.Message);

                _storage.Verify(
                    s => s.SaveFileAsync(
                        It.IsAny<string>(),
                        It.IsAny<string>(),
                        It.IsAny<Stream>(),
                        It.IsAny<IAccessCondition>()),
                    Times.Never);

                _auditing.Verify(
                    a => a.SaveAuditRecordAsync(
                        It.IsAny<FeatureFlagsAuditRecord>()),
                    Times.Never());
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
                await _target.RemoveUserAsync(new User { Username = "user1" });

                // Arrange
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

                _auditing.Verify(
                    a => a.SaveAuditRecordAsync(
                        It.Is<FeatureFlagsAuditRecord>(
                            r => r.Action == AuditedFeatureFlagsAction.Update
                                && r.ContentId == "fake-content-id"
                                && r.Result == ContentSaveResult.Ok
                                && !r.Features.Any()
                                && r.Flights.SingleOrDefault(
                                    f => f.Name == "A"
                                        && !f.All
                                        && !f.SiteAdmins
                                        && f.Accounts.Single() == "user2"
                                        && !f.Domains.Any()) != null
                                && r.Flights.SingleOrDefault(
                                    f => f.Name == "B"
                                        && !f.All
                                        && !f.SiteAdmins
                                        && !f.Accounts.Any()
                                        && !f.Domains.Any()) != null
                                && r.Flights.SingleOrDefault(
                                    f => f.Name == "C"
                                        && !f.All
                                        && !f.SiteAdmins
                                        && f.Accounts.Single() == "user2"
                                        && !f.Domains.Any()) != null)),
                    Times.Once());
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
                await _target.RemoveUserAsync(new User { Username = "user1" });

                // Assert
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

                _auditing.Verify(
                    a => a.SaveAuditRecordAsync(
                        It.Is<FeatureFlagsAuditRecord>(
                            r => r.Action == AuditedFeatureFlagsAction.Update
                                && r.ContentId == "fake-content-id"
                                && r.Result == ContentSaveResult.Conflict
                                && !r.Features.Any()
                                && r.Flights.SingleOrDefault(
                                    f => f.Name == "A"
                                        && !f.All
                                        && !f.SiteAdmins
                                        && f.Accounts.Single() == "user2"
                                        && !f.Domains.Any()) != null)),
                    Times.Once());

                _auditing.Verify(
                    a => a.SaveAuditRecordAsync(
                        It.Is<FeatureFlagsAuditRecord>(
                            r => r.Action == AuditedFeatureFlagsAction.Update
                                && r.ContentId == "fake-content-id"
                                && r.Result == ContentSaveResult.Ok
                                && !r.Features.Any()
                                && r.Flights.SingleOrDefault(
                                    f => f.Name == "A"
                                        && !f.All
                                        && !f.SiteAdmins
                                        && f.Accounts.Single() == "user2"
                                        && !f.Domains.Any()) != null)),
                    Times.Once());
            }

            [Fact]
            public async Task WhenSavePreconditionAlwaysFails_Throws()
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
                var exception = await Assert.ThrowsAsync<InvalidOperationException>(() => _target.RemoveUserAsync(new User { Username = "user1" }));

                // Assert
                Assert.Contains("Unable to remove user from feature flags", exception.Message);

                _storage.Verify(
                    s => s.SaveFileAsync(
                        CoreConstants.Folders.ContentFolderName,
                        CoreConstants.FeatureFlagsFileName,
                        It.IsAny<Stream>(),
                        It.IsAny<IAccessCondition>()),
                    Times.Exactly(3));

                _auditing.Verify(
                    a => a.SaveAuditRecordAsync(
                        It.Is<FeatureFlagsAuditRecord>(
                            r => r.Action == AuditedFeatureFlagsAction.Update
                                && r.ContentId == "fake-content-id"
                                && r.Result == ContentSaveResult.Conflict
                                && !r.Features.Any() 
                                && r.Flights.SingleOrDefault(
                                    f => f.Name == "A" 
                                        && !f.All
                                        && !f.SiteAdmins
                                        && f.Accounts.Single() == "user2" 
                                        && !f.Domains.Any()) != null)),
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
                await _target.RemoveUserAsync(new User { Username = "user1" });

                // Assert
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
            protected readonly Mock<IAuditingService> _auditing;
            protected readonly EditableFeatureFlagFileStorageService _target;
            protected readonly CloudBlobPreconditionFailedException _preconditionException;

            public FactsBase()
            {
                var logger = Mock.Of<ILogger<EditableFeatureFlagFileStorageService>>();

                _storage = new Mock<ICoreFileStorageService>();
                _auditing = new Mock<IAuditingService>();
                _target = new EditableFeatureFlagFileStorageService(
                    _storage.Object, _auditing.Object, logger);

                _preconditionException = new CloudBlobPreconditionFailedException(new Exception());
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
