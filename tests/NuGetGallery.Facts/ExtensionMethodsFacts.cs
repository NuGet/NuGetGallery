﻿// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using NuGet.Frameworks;
using Xunit;

namespace NuGetGallery
{
    public class ExtensionMethodsFacts
    {
        public class TheToFriendlyNameMethod
        {
            [Theory]
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
    }
}
