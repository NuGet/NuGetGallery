// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using NuGet.Frameworks;
using NuGet.Services.Entities;
using System;
using System.Collections.Generic;
using System.Linq;
using Xunit;

namespace NuGetGallery.Frameworks
{
    public class PackageFrameworkCompatibilityFactoryFacts
    {
        private readonly PackageFrameworkCompatibilityFactory _factory;
        private readonly IFrameworkCompatibilityService _service;

        public PackageFrameworkCompatibilityFactoryFacts()
        {
            _service = new FrameworkCompatibilityService();
            _factory = new PackageFrameworkCompatibilityFactory(_service);
        }

        [Fact]
        public void NullPackageFrameworksThrowsArgumentNullException()
        {
            Assert.Throws<ArgumentNullException>(() => _factory.Create(null));
        }

        [Fact]
        public void EmptyPackageFrameworksReturnsEmptyTable()
        {
            var result = _factory.Create(new List<PackageFramework>());

            Assert.Equal(expected: 0, result.Table.Count);
        }

        [Theory]
        [InlineData(FrameworkProductNames.Net, "net5.0", "net6.0", "net6.0-android")]
        [InlineData(FrameworkProductNames.NetCore, "netcoreapp1.0", "netcoreapp3.1")]
        [InlineData(FrameworkProductNames.NetStandard, "netstandard11", "netstandardapp1.5")]
        [InlineData(FrameworkProductNames.NetFramework, "net11", "net472", "net48")]
        [InlineData(FrameworkProductNames.NetMicroFramework, "netmf")]
        [InlineData(FrameworkProductNames.UWP, "netcore50", "uap", "uap10.0")]
        [InlineData(FrameworkProductNames.WindowsPhone, "wp", "wp81", "wpa81")]
        [InlineData(FrameworkProductNames.WindowsStore, "netcore45", "netcore451", "win", "win8", "win81")]
        [InlineData(FrameworkConstants.FrameworkIdentifiers.AspNet, "aspnet50")]
        [InlineData(FrameworkConstants.FrameworkIdentifiers.AspNetCore, "aspnetcore50")]
        [InlineData(FrameworkConstants.FrameworkIdentifiers.Dnx, "dnx45", "dnx452")]
        [InlineData(FrameworkConstants.FrameworkIdentifiers.DnxCore, "dnxcore", "dnxcore50")]
        [InlineData(FrameworkConstants.FrameworkIdentifiers.MonoAndroid, "monoandroid")]
        [InlineData(FrameworkConstants.FrameworkIdentifiers.MonoMac, "monomac")]
        [InlineData(FrameworkConstants.FrameworkIdentifiers.MonoTouch, "monotouch")]
        [InlineData(FrameworkConstants.FrameworkIdentifiers.Native, "native")]
        [InlineData(FrameworkConstants.FrameworkIdentifiers.NetPlatform, "dotnet50", "dotnet52")]
        [InlineData(FrameworkConstants.FrameworkIdentifiers.Silverlight, "sl4", "sl5")]
        [InlineData(FrameworkConstants.FrameworkIdentifiers.Tizen, "tizen3", "tizen6")]
        [InlineData(FrameworkConstants.FrameworkIdentifiers.Windows, "win10")]
        [InlineData(FrameworkConstants.FrameworkIdentifiers.WinRT, "winrt")]
        [InlineData(FrameworkConstants.FrameworkIdentifiers.XamarinIOs, "xamarinios")]
        [InlineData(FrameworkConstants.FrameworkIdentifiers.XamarinMac, "xamarinmac")]
        [InlineData(FrameworkConstants.FrameworkIdentifiers.XamarinPlayStation3, "xamarinpsthree")]
        [InlineData(FrameworkConstants.FrameworkIdentifiers.XamarinPlayStation4, "xamarinpsfour")]
        [InlineData(FrameworkConstants.FrameworkIdentifiers.XamarinPlayStationVita, "xamarinplaystationvita")]
        [InlineData(FrameworkConstants.FrameworkIdentifiers.XamarinTVOS, "xamarintvos")]
        [InlineData(FrameworkConstants.FrameworkIdentifiers.XamarinWatchOS, "xamarinwatchos")]
        [InlineData(FrameworkConstants.FrameworkIdentifiers.XamarinXbox360, "xamarinxboxthreesixty")]
        [InlineData(FrameworkConstants.FrameworkIdentifiers.XamarinXboxOne, "xamarinxboxone")]
        public void FrameworksShouldOnlyBeOnASingleProductFrameworkName(string productName, params string[] frameworks)
        {
            var packageFrameworks = new HashSet<PackageFramework>();
            foreach (var framework in frameworks)
            {
                var packageFramework = new PackageFramework()
                {
                    TargetFramework = framework
                };
                packageFrameworks.Add(packageFramework);
            }

            var results = _factory.Create(packageFrameworks.ToList());

            Assert.True(results.Table.TryGetValue(productName, out var compatibleFrameworks));
            Assert.True(compatibleFrameworks.Count > 0);
            foreach (var row in results.Table)
            {
                foreach (var packgeFramework in packageFrameworks)
                {
                    if (productName.Equals(row.Key))
                    {
                        Assert.True(row.Value.Any(f => f.Framework == packgeFramework.FrameworkName));
                    }
                    else
                    {
                        Assert.False(row.Value.Any(f => f.Framework == packgeFramework.FrameworkName));
                    }
                }
            }
        }

        [Fact]
        public void EmptyPackageFrameworksReturnsNullBadges()
        {
            var result = _factory.Create(new List<PackageFramework>());

            Assert.Null(result.Badges.Net);
            Assert.Null(result.Badges.NetCore);
            Assert.Null(result.Badges.NetStandard);
            Assert.Null(result.Badges.NetFramework);
        }
    }
}
