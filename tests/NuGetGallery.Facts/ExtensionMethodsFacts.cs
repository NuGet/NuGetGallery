// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Versioning;
using System.Text;
using System.Threading.Tasks;
using NuGet;
using Xunit;
using Xunit.Extensions;

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
            public void CorrectlyConvertsShortNameToFriendlyName(string expected, string shortName)
            {
                var fx = VersionUtility.ParseFrameworkName(shortName);
                var actual = fx.ToFriendlyName();
                Assert.Equal(expected, actual);
            }
        }
    }
}
