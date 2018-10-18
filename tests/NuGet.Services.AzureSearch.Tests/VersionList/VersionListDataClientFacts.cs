// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Moq;
using NuGetGallery;
using Xunit;

namespace NuGet.Services.AzureSearch
{
    public class VersionListDataClientFacts
    {
        public class ReadVersionListAsync : Facts
        {
            [Fact]
            public async Task ReturnsEmptyListWhenFileDoesNotExist()
            {
                var output = await _target.ReadAsync(_id);

                Assert.NotNull(output);
                Assert.NotNull(output.Result);
                Assert.NotNull(output.AccessCondition);
                Assert.NotNull(output.Result.VersionProperties);
                Assert.Empty(output.Result.VersionProperties);
                Assert.Equal("*", output.AccessCondition.IfNoneMatchETag);
                Assert.Null(output.AccessCondition.IfMatchETag);
            }

            [Fact]
            public async Task UsesExpectedContainerAndFileName()
            {
                await _target.ReadAsync(_id);

                _storageService.Verify(
                    x => x.GetFileReferenceAsync(
                        "content",
                        "version-lists/nuget.versioning.json",
                        It.IsAny<string>()),
                    Times.Once);
            }

            [Fact]
            public async Task DeserializesJson()
            {
                var version2 = "2.0.0-beta.2";
                var version10 = "10.0.0";
                var versionList = Encoding.UTF8.GetBytes(@"{
  ""VersionProperties"": {
    """ + version10 + @""": {},
    """ + version2 + @""": {
      ""Listed"": true,
      ""SemVer2"": true
    }
  }
}");
                var etag = "\"some-etag\"";
                var fileReference = new Mock<IFileReference>();
                fileReference
                    .Setup(x => x.OpenRead())
                    .Returns(() => new MemoryStream(versionList));
                fileReference
                    .Setup(x => x.ContentId)
                    .Returns(etag);
                _storageService
                    .Setup(x => x.GetFileReferenceAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
                    .ReturnsAsync(() => fileReference.Object);

                var output = await _target.ReadAsync(_id);

                Assert.NotNull(output);
                Assert.NotNull(output.Result);
                Assert.NotNull(output.AccessCondition);
                Assert.NotNull(output.Result.VersionProperties);
                Assert.Equal(etag, output.AccessCondition.IfMatchETag);
                Assert.Null(output.AccessCondition.IfNoneMatchETag);
                var versions = output.Result.VersionProperties;
                Assert.Equal(2, versions.Count);
                Assert.Equal(new[] { version10, version2 }, versions.Keys.OrderBy(x => x).ToArray());
                Assert.True(versions[version2].Listed);
                Assert.True(versions[version2].SemVer2);
                Assert.False(versions[version10].Listed);
                Assert.False(versions[version10].SemVer2);
            }
        }

        public class ReplaceVersionListAsync : Facts
        {
            [Fact]
            public async Task UsesExpectedStorageParameters()
            {
                await _target.ReplaceAsync(_id, _versionList, _accessCondition.Object);

                _storageService.Verify(
                    x => x.SaveFileAsync(
                        "content",
                        "version-lists/nuget.versioning.json",
                        It.IsAny<Stream>(),
                        _accessCondition.Object),
                    Times.Once);
            }

            [Fact]
            public async Task SerializesWithoutBOM()
            {
                await _target.ReplaceAsync(_id, _versionList, _accessCondition.Object);

                var bytes = Assert.Single(_savedBytes);
                Assert.Equal((byte)'{', bytes[0]);
            }

            [Fact]
            public async Task SerializesVersionsInSemVerOrder()
            {
                var versionList = new VersionListData(new Dictionary<string, VersionPropertiesData>
                {
                    { "2.0.0", new VersionPropertiesData(listed: true, semVer2: false) },
                    { "1.0.0-beta.2", new VersionPropertiesData(listed: true, semVer2: true) },
                    { "10.0.0", new VersionPropertiesData(listed: false, semVer2: false) },
                    { "1.0.0-beta.10", new VersionPropertiesData(listed: true, semVer2: true) },
                });

                await _target.ReplaceAsync(_id, versionList, _accessCondition.Object);

                Assert.Single(_savedStrings);
                Assert.Equal(@"{
  ""VersionProperties"": {
    ""1.0.0-beta.2"": {
      ""Listed"": true,
      ""SemVer2"": true
    },
    ""1.0.0-beta.10"": {
      ""Listed"": true,
      ""SemVer2"": true
    },
    ""2.0.0"": {
      ""Listed"": true
    },
    ""10.0.0"": {}
  }
}", _savedStrings[0]);
            }
        }

        public abstract class Facts
        {
            protected readonly string _id;
            protected readonly Mock<IAccessCondition> _accessCondition;
            protected readonly VersionListData _versionList;
            protected readonly Mock<ICoreFileStorageService> _storageService;
            protected readonly VersionListDataClient _target;

            protected readonly List<byte[]> _savedBytes = new List<byte[]>();
            protected readonly List<string> _savedStrings = new List<string>();

            public Facts()
            {
                _id = "NuGet.Versioning";
                _accessCondition = new Mock<IAccessCondition>();
                _versionList = new VersionListData(new Dictionary<string, VersionPropertiesData>
                {
                    { "2.0.0", new VersionPropertiesData(listed: true, semVer2: false) },
                });

                _storageService = new Mock<ICoreFileStorageService>();

                _storageService
                    .Setup(x => x.SaveFileAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<Stream>(), It.IsAny<IAccessCondition>()))
                    .Returns(Task.CompletedTask)
                    .Callback<string, string, Stream, IAccessCondition>((_, __, s, ___) =>
                    {
                        using (s)
                        using (var buffer = new MemoryStream())
                        {
                            s.CopyTo(buffer);
                            var bytes = buffer.ToArray();
                            _savedBytes.Add(bytes);
                            _savedStrings.Add(Encoding.UTF8.GetString(bytes));
                        }
                    });

                _target = new VersionListDataClient(_storageService.Object);
            }
        }
    }
}
