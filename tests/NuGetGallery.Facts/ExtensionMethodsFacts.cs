// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using Microsoft.Owin;
using Moq;
using NuGet.Frameworks;
using System.Security.Principal;
using Xunit;

namespace NuGetGallery
{
    public class ExtensionMethodsFacts
    {
        public class TheToFriendlyNameMethod
        {
            [Theory]
            [InlineData("net5.0", "net5.0")]
            [InlineData("net5.0", "NET5.0")]
            [InlineData("net5.0", "net5")]
            [InlineData("net5.0", "net50")]
            [InlineData("net5.0", "netcoreapp5.0")]
            [InlineData("net5.0", "netcoreapp50")]
            [InlineData("net5.0-windows", "net5.0-windows")]
            [InlineData("net5.0-windows9.0", "net5.0-windows9")]
            [InlineData("net5.0-ios14.0", "net5.0-ios14.0")]
            [InlineData("net5.0-windows", "netcoreapp5.0-windows")]
            [InlineData("net5.0-windows9.0", "netcoreapp5.0-windows9")]
            [InlineData("net6.0", "net6.0")]
            [InlineData("net10.0", "net10.0")]
            [InlineData(".NETFramework 4.0", "net40")]
            [InlineData("Silverlight 4.0", "sl40")]
            [InlineData("WindowsPhone 8.0", "wp8")]
            [InlineData("Windows 8.1", "win81")]
            [InlineData("Portable Class Library (.NETFramework 4.0, Silverlight 4.0, Windows 8.0, WindowsPhone 7.1)", "portable-net40+sl4+win8+wp71")]
            [InlineData("Portable Class Library (.NETFramework 4.5, Windows 8.0)", "portable-net45+win8")]
            [InlineData("Portable Class Library (.NETFramework 4.0, Windows 8.0)", "portable40-net40+win8")]
            [InlineData("Portable Class Library (.NETFramework 4.5, Windows 8.0)", "portable45-net45+win8")]
            public void CorrectlyConvertsShortNameToFriendlyName(string expected, string shortName)
            {
                var fx = NuGetFramework.Parse(shortName);
                var actual = fx.ToFriendlyName();
                Assert.Equal(expected, actual);
            }

            [Theory]
            [InlineData(".NETFramework 4.0", "net40")]
            [InlineData("Silverlight 4.0", "sl40")]
            [InlineData("WindowsPhone 8.0", "wp8")]
            [InlineData("Windows 8.1", "win81")]
            [InlineData("Portable Class Library", "portable-net40+sl4+win8+wp71")]
            [InlineData("Portable Class Library", "portable-net45+win8")]
            [InlineData("Portable Class Library", "portable40-net45+win8")]
            [InlineData("Portable Class Library", "portable45-net45+win8")]
            public void DoesNotRecurseWhenAllowRecurseProfileFalse(string expected, string shortName)
            {
                var fx = NuGetFramework.Parse(shortName);
                var actual = fx.ToFriendlyName(allowRecurseProfile: false);
                Assert.Equal(expected, actual);
            }
        }

        public class TheGetCurrentUserMethod
        {
            [Fact]
            public void ReturnsNullIfNullUser()
            {
                // Arrange
                var mockOwinRequest = new Mock<IOwinRequest>();
                mockOwinRequest.Setup(x => x.User).Returns<IPrincipal>(null);

                var mockOwinContext = new Mock<IOwinContext>();
                mockOwinContext.Setup(x => x.Request).Returns(mockOwinRequest.Object);

                // Act
                var currentUser = mockOwinContext.Object.GetCurrentUser();

                // Assert
                Assert.Null(currentUser);
            }

            [Fact]
            public void ReturnsNullIfUnauthenticatedUser()
            {
                // Arrange
                var mockIdentity = new Mock<IIdentity>();
                mockIdentity.Setup(x => x.IsAuthenticated).Returns(false);

                var mockPrincipal = new Mock<IPrincipal>();
                mockPrincipal.Setup(x => x.Identity).Returns(mockIdentity.Object);

                var mockOwinRequest = new Mock<IOwinRequest>();
                mockOwinRequest.Setup(x => x.User).Returns(mockPrincipal.Object);

                var mockOwinContext = new Mock<IOwinContext>();
                mockOwinContext.Setup(x => x.Request).Returns(mockOwinRequest.Object);

                // Act
                var currentUser = mockOwinContext.Object.GetCurrentUser();

                // Assert
                Assert.Null(currentUser);
            }
        }
    }
}
