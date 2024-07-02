// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Moq;
using Newtonsoft.Json;
using NuGetGallery.Shared;
using Xunit;

namespace NuGetGallery.Login
{
    public class EditableLoginConfigurationFileStorageServiceFacts
    {
        public static LoginDiscontinuation Example = new LoginDiscontinuation(
            new[] { "cannotUsePassword@canUsePassword.com" },
            new[] { "cannotUsePassword.com" },
            new[] { "exception@cannotUsePassword.com" },
            new[] { "organization@cannotUsePassword.com" },
            new[] { new OrganizationTenantPair("tenantOnly.com", "tenantID") },
            isPasswordDiscontinuedForAll: false);

        public const string ExampleJson = @"{
  ""IsPasswordDiscontinuedForAll"": false,
  ""DiscontinuedForEmailAddresses"": [
    ""cannotUsePassword@canUsePassword.com""
  ],
  ""DiscontinuedForDomains"": [
    ""cannotUsePassword.com""
  ],
  ""ExceptionsForEmailAddresses"": [
    ""exception@cannotUsePassword.com""
  ],
  ""ForceTransformationToOrganizationForEmailAddresses"": [
    ""organization@cannotUsePassword.com""
  ],
  ""EnabledOrganizationAadTenants"": [
    {
      ""EmailDomain"": ""tenantOnly.com"",
      ""TenantId"": ""tenantID""
    }
  ]
}";

        public static void AssertExample(LoginDiscontinuation actual)
        {
            Assert.Equal(actual.ExceptionsForEmailAddresses.Count, 1);
            Assert.True(actual.ExceptionsForEmailAddresses.Contains("exception@cannotUsePassword.com"));
            Assert.Equal(actual.DiscontinuedForDomains.Count, 1);
            Assert.True(actual.DiscontinuedForDomains.Contains("cannotUsePassword.com"));
            Assert.Equal(actual.DiscontinuedForEmailAddresses.Count, 1);
            Assert.True(actual.DiscontinuedForEmailAddresses.Contains("cannotUsePassword@canUsePassword.com"));
            Assert.Equal(actual.ForceTransformationToOrganizationForEmailAddresses.Count, 1);
            Assert.True(actual.ForceTransformationToOrganizationForEmailAddresses.Contains("organization@cannotUsePassword.com"));
            Assert.Equal(actual.EnabledOrganizationAadTenants.Count, 1);
            Assert.True(actual.EnabledOrganizationAadTenants.Contains(new OrganizationTenantPair("tenantOnly.com", "tenantID")));
        }

        public class GetAsync : FactsBase
        {
            [Fact]
            public async Task DeserializesLoginConfiguration()
            {
                // Arrange
                _storage
                    .Setup(s => s.GetFileAsync(CoreConstants.Folders.ContentFolderName, CoreConstants.LoginDiscontinuationConfigFileName))
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
                    .Setup(s => s.GetFileAsync(CoreConstants.Folders.ContentFolderName, CoreConstants.LoginDiscontinuationConfigFileName))
                    .ReturnsAsync(BuildStream("Bad"));

                await Assert.ThrowsAsync<JsonReaderException>(() => _target.GetAsync());
            }
        }

        public class GetReferenceAsync : FactsBase
        {
            [Fact]
            public async Task GetsAndFormatsLoginDiscontinuationConfig()
            {
                // Arrange
                _storage
                    .Setup(s => s.GetFileReferenceAsync(CoreConstants.Folders.ContentFolderName, CoreConstants.LoginDiscontinuationConfigFileName, null))
                    .ReturnsAsync(BuildFileReference(ExampleJson, "bar"));

                // Act
                var result = await _target.GetReferenceAsync();

                // Assert - the login should be formatted
                AssertExample(result.Logins);
                Assert.Equal("bar", result.ContentId);

                _storage
                    .Verify(
                        s => s.GetFileReferenceAsync(CoreConstants.Folders.ContentFolderName, CoreConstants.LoginDiscontinuationConfigFileName, null),
                        Times.Once);
            }

            [Fact]
            public async Task ThrowsOnInvalidLoginDiscontinuationConfig()
            {
                // Arrange
                _storage
                    .Setup(s => s.GetFileReferenceAsync(CoreConstants.Folders.ContentFolderName, CoreConstants.LoginDiscontinuationConfigFileName, null))
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
                        CoreConstants.LoginDiscontinuationConfigFileName,
                        It.IsAny<Stream>(),
                        It.Is<IAccessCondition>(c => c.IfNoneMatchETag == null && c.IfMatchETag != null)),
                    Times.Once);
            }

            [Fact]
            public async Task FormatsSavedJson()
            {
                // Arrange
                string json = null;
                _storage.Setup(
                    s => s.SaveFileAsync(
                        CoreConstants.Folders.ContentFolderName,
                        CoreConstants.LoginDiscontinuationConfigFileName,
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
                        CoreConstants.LoginDiscontinuationConfigFileName,
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
                        CoreConstants.LoginDiscontinuationConfigFileName,
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
                        CoreConstants.LoginDiscontinuationConfigFileName,
                        It.IsAny<Stream>(),
                        It.Is<IAccessCondition>(c => c.IfNoneMatchETag == null && c.IfMatchETag != null)),
                    Times.Once);
            }
        }

        public class AddOrRemoveUserEmailAddressforPasswordAuthenticationMethod : FactsBase
        {
            [Fact]
            public async Task AddUserEmailAddressWhenInList_DoesntSave()
            {
                _storage
                    .Setup(s => s.GetFileReferenceAsync(CoreConstants.Folders.ContentFolderName, CoreConstants.LoginDiscontinuationConfigFileName, null))
                    .ReturnsAsync(BuildFileReference(ExampleJson, "bar"));

                await _target.AddUserEmailAddressforPasswordAuthenticationAsync("exception@cannotUsePassword.com", ContentOperations.Add);

                // Assert
                _storage.Verify(
                    s => s.SaveFileAsync(
                        It.IsAny<string>(),
                        It.IsAny<string>(),
                        It.IsAny<Stream>(),
                        It.IsAny<IAccessCondition>()),
                    Times.Never);
            }

            [Fact]
            public async Task RemoveUserEmailAddressWhenNotInList_DoesntSave()
            {
                _storage
                    .Setup(s => s.GetFileReferenceAsync(CoreConstants.Folders.ContentFolderName, CoreConstants.LoginDiscontinuationConfigFileName, null))
                    .ReturnsAsync(BuildFileReference(ExampleJson, "bar"));

                await _target.AddUserEmailAddressforPasswordAuthenticationAsync("exception@cannotUse.com", ContentOperations.Remove);

                // Assert
                _storage.Verify(
                    s => s.SaveFileAsync(
                        It.IsAny<string>(),
                        It.IsAny<string>(),
                        It.IsAny<Stream>(),
                        It.IsAny<IAccessCondition>()),
                    Times.Never);
            }

            [Fact]
            public async Task AddUserEmailAddressToExceptionList()
            {
                string savedJson = null;
                var loginDiscontinuation = new LoginDiscontinuation(
                    new[] { "cannotUsePassword@canUsePassword.com" },
                    new[] { "cannotUsePassword.com" },
                    new[] { "exception@cannotUsePassword.com" },
                    new[] { "organization@cannotUsePassword.com" },
                    new[] { new OrganizationTenantPair("tenantOnly.com", "tenantID") },
                    isPasswordDiscontinuedForAll: false);

                _storage
                    .Setup(s => s.GetFileReferenceAsync(CoreConstants.Folders.ContentFolderName, CoreConstants.LoginDiscontinuationConfigFileName, null))
                    .ReturnsAsync(BuildFileReference(loginDiscontinuation));

                _storage
                    .Setup(s => s.SaveFileAsync(
                        CoreConstants.Folders.ContentFolderName,
                        CoreConstants.LoginDiscontinuationConfigFileName,
                        It.IsAny<Stream>(),
                        It.IsAny<IAccessCondition>()))
                    .Callback((string folder, string file, Stream content, IAccessCondition condition) =>
                    {
                        savedJson = ToString(content);
                    })
                    .Returns(Task.CompletedTask);

                await _target.AddUserEmailAddressforPasswordAuthenticationAsync("example@password.com", ContentOperations.Add);

                Assert.NotNull(savedJson);

                var savedLoginDiscontinuation = JsonConvert.DeserializeObject<LoginDiscontinuation>(savedJson);

                Assert.Equal(savedLoginDiscontinuation.ExceptionsForEmailAddresses.Count, 2);
                Assert.True(savedLoginDiscontinuation.ExceptionsForEmailAddresses.Contains("example@password.com"));

                _storage.Verify(
                    s => s.SaveFileAsync(
                        CoreConstants.Folders.ContentFolderName,
                        CoreConstants.LoginDiscontinuationConfigFileName,
                        It.IsAny<Stream>(),
                        It.IsAny<IAccessCondition>()),
                    Times.Once);
            }

            [Fact]
            public async Task RemoveUserEmailAddressToExceptionList()
            {
                string savedJson = null;
                var loginDiscontinuation = new LoginDiscontinuation(
                    new[] { "cannotUsePassword@canUsePassword.com" },
                    new[] { "cannotUsePassword.com" },
                    new[] { "exception@cannotUsePassword.com" },
                    new[] { "organization@cannotUsePassword.com" },
                    new[] { new OrganizationTenantPair("tenantOnly.com", "tenantID") },
                    isPasswordDiscontinuedForAll: false);

                _storage
                    .Setup(s => s.GetFileReferenceAsync(CoreConstants.Folders.ContentFolderName, CoreConstants.LoginDiscontinuationConfigFileName, null))
                    .ReturnsAsync(BuildFileReference(loginDiscontinuation));

                _storage
                    .Setup(s => s.SaveFileAsync(
                        CoreConstants.Folders.ContentFolderName,
                        CoreConstants.LoginDiscontinuationConfigFileName,
                        It.IsAny<Stream>(),
                        It.IsAny<IAccessCondition>()))
                    .Callback((string folder, string file, Stream content, IAccessCondition condition) =>
                    {
                        savedJson = ToString(content);
                    })
                    .Returns(Task.CompletedTask);

                await _target.AddUserEmailAddressforPasswordAuthenticationAsync("exception@cannotUsePassword.com", ContentOperations.Remove);

                Assert.NotNull(savedJson);

                var savedLoginDiscontinuation = JsonConvert.DeserializeObject<LoginDiscontinuation>(savedJson);

                Assert.Equal(savedLoginDiscontinuation.ExceptionsForEmailAddresses.Count, 0);

                _storage.Verify(
                    s => s.SaveFileAsync(
                        CoreConstants.Folders.ContentFolderName,
                        CoreConstants.LoginDiscontinuationConfigFileName,
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
                var loginDiscontinuation = new LoginDiscontinuation(
                    new[] { "cannotUsePassword@canUsePassword.com" },
                    new[] { "cannotUsePassword.com" },
                    new[] { "exception@cannotUsePassword.com" },
                    new[] { "organization@cannotUsePassword.com" },
                    new[] { new OrganizationTenantPair("tenantOnly.com", "tenantID") },
                    isPasswordDiscontinuedForAll: false);

                _storage
                    .Setup(s => s.GetFileReferenceAsync(CoreConstants.Folders.ContentFolderName, CoreConstants.LoginDiscontinuationConfigFileName, null))
                    .ReturnsAsync(BuildFileReference(loginDiscontinuation));

                _storage
                    .Setup(s => s.SaveFileAsync(
                        CoreConstants.Folders.ContentFolderName,
                        CoreConstants.LoginDiscontinuationConfigFileName,
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
                await _target.AddUserEmailAddressforPasswordAuthenticationAsync("example@password.com", ContentOperations.Add);

                // Assert
                Assert.NotNull(savedJson);

                Assert.NotNull(savedJson);

                var savedLoginDiscontinuation = JsonConvert.DeserializeObject<LoginDiscontinuation>(savedJson);

                Assert.Equal(savedLoginDiscontinuation.ExceptionsForEmailAddresses.Count, 2);
                Assert.True(savedLoginDiscontinuation.ExceptionsForEmailAddresses.Contains("example@password.com"));

                _storage.Verify(
                    s => s.SaveFileAsync(
                        CoreConstants.Folders.ContentFolderName,
                        CoreConstants.LoginDiscontinuationConfigFileName,
                        It.IsAny<Stream>(),
                        It.IsAny<IAccessCondition>()),
                    Times.Exactly(2));
            }

            [Fact]
            public async Task WhenSavePreconditionAlwaysFails_Throws()
            {
                // Arrange
                var loginDiscontinuation = new LoginDiscontinuation(
                    new[] { "cannotUsePassword@canUsePassword.com" },
                    new[] { "cannotUsePassword.com" },
                    new[] { "exception@cannotUsePassword.com" },
                    new[] { "organization@cannotUsePassword.com" },
                    new[] { new OrganizationTenantPair("tenantOnly.com", "tenantID") },
                    isPasswordDiscontinuedForAll: false);

                _storage
                    .Setup(s => s.GetFileReferenceAsync(CoreConstants.Folders.ContentFolderName, CoreConstants.LoginDiscontinuationConfigFileName, null))
                    .ReturnsAsync(BuildFileReference(loginDiscontinuation));

                _storage
                    .Setup(s => s.SaveFileAsync(
                        CoreConstants.Folders.ContentFolderName,
                        CoreConstants.LoginDiscontinuationConfigFileName,
                        It.IsAny<Stream>(),
                        It.IsAny<IAccessCondition>()))
                    .ThrowsAsync(_preconditionException);

                // Act
                var exception = await Assert.ThrowsAsync<InvalidOperationException>(() => _target.AddUserEmailAddressforPasswordAuthenticationAsync("example@password.com", ContentOperations.Add));

                // Assert
                Assert.Contains("Unable to add/remove emailAddress from exception list", exception.Message);

                _storage.Verify(
                    s => s.SaveFileAsync(
                        CoreConstants.Folders.ContentFolderName,
                        CoreConstants.LoginDiscontinuationConfigFileName,
                        It.IsAny<Stream>(),
                        It.IsAny<IAccessCondition>()),
                    Times.Exactly(3));
            }
        }

        public class GetListOfExceptionEmailListMethod: FactsBase
        {
            [Fact]
            public async Task GetListOfExceptionEmailListSuccessfully()
            {
                _storage
                    .Setup(s => s.GetFileAsync(CoreConstants.Folders.ContentFolderName, CoreConstants.LoginDiscontinuationConfigFileName))
                    .ReturnsAsync(BuildStream(ExampleJson));

                // Act
                var result = await _target.GetListOfExceptionEmailList();

                Assert.NotNull(result);
                Assert.True(result.Contains("exception@cannotUsePassword.com"));
            }
        }
        public class FactsBase
        {
            protected readonly Mock<ICoreFileStorageService> _storage;
            protected readonly EditableLoginConfigurationFileStorageService _target;
            protected readonly CloudBlobPreconditionFailedException _preconditionException;

            public FactsBase()
            {
                var logger = Mock.Of<ILogger<EditableLoginConfigurationFileStorageService>>();

                _storage = new Mock<ICoreFileStorageService>();
                _target = new EditableLoginConfigurationFileStorageService(
                    _storage.Object, logger);

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

            protected IFileReference BuildFileReference(LoginDiscontinuation content, string contentId = null)
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
