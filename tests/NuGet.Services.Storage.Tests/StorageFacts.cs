// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using Xunit;

namespace NuGet.Services.Storage.Tests
{
    public class StorageFacts
    {
        public class TheGetNameMethod
        {
            [Theory]
            [InlineData("https://example.com/storage/file.txt", "file.txt")]
            [InlineData("https://example.com/storage/foo/bar/file.txt", "foo/bar/file.txt")]
            public void ExtractsNameFromUri(string uri, string expectedName)
            {
                var result = Storage.GetName(new Uri("https://example.com/storage/"), new Uri(uri));
                Assert.Equal(expectedName, result);
            }

            [Theory]
            [InlineData("https://example.com/storage", "https://example.com/storage/file.txt", "file.txt")]
            [InlineData("https://example.com/storage/", "https://example.com/storage/file.txt", "file.txt")]
            public void HandlesBaseAddressWithAndWithoutTrailingSlash(string baseAddress, string uri, string expectedName)
            {
                var result = Storage.GetName(new Uri(baseAddress), new Uri(uri));
                Assert.Equal(expectedName, result);
            }

            [Theory]
            [InlineData("http://example.com/storage/", "https://example.com/storage/file.txt", "file.txt")]
            [InlineData("https://example.com/storage/", "http://example.com/storage/file.txt", "file.txt")]
            public void HandlesSchemeMismatch(string baseAddress, string uri, string expectedName)
            {
                var result = Storage.GetName(new Uri(baseAddress), new Uri(uri));
                Assert.Equal(expectedName, result);
            }

            [Theory]
            [InlineData("https://example.com/storage/", "https://example.com/storage/file%20name.txt", "file name.txt")]
            [InlineData("https://example.com/storage/", "https://example.com/storage/%E6%96%87%E4%BB%B6.txt", "文件.txt")]
            [InlineData("https://example.com/storage%20with%20spaces/", "https://example.com/storage%20with%20spaces/file.txt", "file.txt")]
            public void HandlesUrlEncodedCharacters(string baseAddress, string uri, string expectedName)
            {
                var result = Storage.GetName(new Uri(baseAddress), new Uri(uri));
                Assert.Equal(expectedName, result);
            }

            [Theory]
            [InlineData("https://example.com/storage/", "https://example.com/storage/file.txt?sv=2021-01-01&sig=abc", "file.txt")]
            [InlineData("https://example.com/storage/?sv=2021-01-01", "https://example.com/storage/file.txt?sig=xyz", "file.txt")]
            public void IgnoresQueryStrings(string baseAddress, string uri, string expectedName)
            {
                var result = Storage.GetName(new Uri(baseAddress), new Uri(uri));
                Assert.Equal(expectedName, result);
            }

            [Fact]
            public void ThrowsArgumentNullExceptionWhenUriIsNull()
            {
                var exception = Assert.Throws<ArgumentNullException>(() => Storage.GetName(new Uri("https://example.com/storage/"), null));
                Assert.Equal("uri", exception.ParamName);
            }

            [Fact]
            public void ThrowsArgumentNullExceptionWhenBaseAddressIsNull()
            {
                var exception = Assert.Throws<ArgumentNullException>(() => Storage.GetName(null, new Uri("https://example.com/storage/file.txt")));
                Assert.Equal("baseAddress", exception.ParamName);
            }

            [Fact]
            public void ThrowsArgumentExceptionWhenUriIsNotAbsolute()
            {
                var exception = Assert.Throws<ArgumentException>(() => Storage.GetName(new Uri("https://example.com/storage/"), new Uri("file.txt", UriKind.Relative)));
                Assert.Equal("uri", exception.ParamName);
                Assert.Contains("must be an absolute URI", exception.Message);
            }

            [Fact]
            public void ThrowsArgumentExceptionWhenBaseAddressIsNotAbsolute()
            {
                var exception = Assert.Throws<ArgumentException>(() => Storage.GetName(new Uri("storage/", UriKind.Relative), new Uri("https://example.com/storage/file.txt")));
                Assert.Equal("baseAddress", exception.ParamName);
                Assert.Contains("must be an absolute URI", exception.Message);
            }

            [Fact]
            public void ThrowsArgumentExceptionWhenUriDoesNotStartWithBaseAddress()
            {
                var exception = Assert.Throws<ArgumentException>(() => Storage.GetName(new Uri("https://example.com/storage/"), new Uri("https://example.com/other/file.txt")));
                Assert.Equal("uri", exception.ParamName);
                Assert.Contains("must start with the base address", exception.Message);
            }

            [Theory]
            [InlineData("https://example.com/storage/", "https://example.com/storage/file.txt#fragment")]
            [InlineData("https://example.com/storage/", "https://example.com/storage/file.txt?query=value")]
            [InlineData("https://example.com/storage/", "https://example.com/storage/file.txt?query=value#fragment")]
            public void StripsFragmentsAndQueryStringsFromUri(string baseAddress, string uri)
            {
                var result = Storage.GetName(new Uri(baseAddress), new Uri(uri));
                Assert.Equal("file.txt", result);
            }

            [Theory]
            [InlineData("https://example.com/storage/", "https://example.com/storage/.hidden/file.txt")]
            [InlineData("https://example.com/storage/", "https://example.com/storage/folder./file.txt")]
            [InlineData("https://example.com/storage/", "https://example.com/storage/foo%5Cbar/file.txt")]
            [InlineData("https://example.com/storage/", "https://example.com/storage/foo%2Fbar/file.txt")]
            public void ThrowsInvalidOperationExceptionForInvalidBlobNames(string baseAddress, string uri)
            {
                Assert.Throws<ArgumentException>(() => Storage.GetName(new Uri(baseAddress), new Uri(uri)));
            }
        }

        public class TheResolveUriMethod
        {
            [Fact]
            public void TrimsSlashOnBaseUri()
            {
                var result = Storage.ResolveUri(new Uri("https://example.com/storage/"), "file.txt");
                Assert.Equal("https://example.com/storage/file.txt", result.AbsoluteUri);
            }

            [Theory]
            [InlineData("file.txt", "https://example.com/storage/file.txt")]
            [InlineData("/file.txt", "https://example.com/storage/file.txt")]
            [InlineData("foo/file.txt", "https://example.com/storage/foo/file.txt")]
            [InlineData("foo/bar/file.txt", "https://example.com/storage/foo/bar/file.txt")]
            public void ResolvesRelativeUri(string relativeUri, string expectedUri)
            {
                var result = Storage.ResolveUri(new Uri("https://example.com/storage"), relativeUri);
                Assert.Equal(expectedUri, result.AbsoluteUri);
            }

            [Theory]
            [InlineData("https://example.com/other/file.txt")]
            [InlineData("ftp://example.com/file.txt")]
            [InlineData("file:///C:/file.txt")]
            [InlineData("../foo.txt")]
            [InlineData("bar//foo.txt")]
            [InlineData("bar\\foo.txt")]
            [InlineData("bar%5Cfoo.txt")]
            [InlineData("bar%2Ffoo.txt")]
            [InlineData("foo/../bar.txt")]
            [InlineData("foo/./bar.txt")]
            [InlineData("foo/.nupkg/bar.txt")]
            [InlineData("foo/nupkg./bar.txt")]
            [InlineData("foo.txt#bar")]
            [InlineData("foo.txt?version=1.0")]
            public void RejectsInvalidRelativeUri(string invalidUri)
            {
                Assert.Throws<ArgumentException>(() => Storage.ResolveUri(new Uri("https://example.com/storage/"), invalidUri));
            }
        }
    }
}
