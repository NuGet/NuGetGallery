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
        public void NullFrameworkCompatibilityServiceThrowsArgumentNullException()
        {
            Assert.Throws<ArgumentNullException>(() => new PackageFrameworkCompatibilityFactory(null));
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

            Assert.Empty(result.Table);
        }

        [Fact]
        public void NonSupportedFrameworksShouldBeIgnored()
        {
            var packageFrameworks = new HashSet<PackageFramework>()
            {
                new PackageFramework() { TargetFramework = "101" },
                new PackageFramework() { TargetFramework = "tfm" },
                new PackageFramework() { TargetFramework = "unity" },
                new PackageFramework() { TargetFramework = "x64" }
            };

            var result = _factory.Create(packageFrameworks.ToList());

            Assert.Empty(result.Table);
            Assert.Null(result.Badges.Net);
            Assert.Null(result.Badges.NetCore);
            Assert.Null(result.Badges.NetStandard);
            Assert.Null(result.Badges.NetFramework);
        }

        [Fact]
        public void PortableClassLibrariesFrameworksShouldBeIgnored()
        {
            var packageFrameworks = new HashSet<PackageFramework>()
            {
                new PackageFramework() { TargetFramework = "portable-net45+sl4+win8+wp7" },
                new PackageFramework() { TargetFramework = "portable-net40+sl4" },
                new PackageFramework() { TargetFramework = "portable-net45+sl5+win8+wpa81+wp8" }
            };

            var result = _factory.Create(packageFrameworks.ToList());

            Assert.Empty(result.Table);
            Assert.Null(result.Badges.Net);
            Assert.Null(result.Badges.NetCore);
            Assert.Null(result.Badges.NetStandard);
            Assert.Null(result.Badges.NetFramework);
        }

        [Fact]
        public void AnyFrameworksShouldBeIgnored()
        {
            var packageFrameworks = new HashSet<PackageFramework>()
            {
                new PackageFramework() { TargetFramework = "any" },
            };

            var result = _factory.Create(packageFrameworks.ToList());

            Assert.Empty(result.Table);
            Assert.Null(result.Badges.Net);
            Assert.Null(result.Badges.NetCore);
            Assert.Null(result.Badges.NetStandard);
            Assert.Null(result.Badges.NetFramework);
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

            var result = _factory.Create(packageFrameworks.ToList());

            Assert.True(result.Table.TryGetValue(productName, out var compatibleFrameworks));
            Assert.NotEmpty(compatibleFrameworks);
            foreach (var row in result.Table)
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
        public void NetBasedFrameworksShouldBeOrderedOnTheFirstRows()
        {
            var packageFrameworks = new HashSet<PackageFramework>()
            {
                new PackageFramework() { TargetFramework = "aspnet50" },
                new PackageFramework() { TargetFramework = "native" },
                new PackageFramework() { TargetFramework = "net45" },
                new PackageFramework() { TargetFramework = "net6" },
                new PackageFramework() { TargetFramework = "netcoreapp31" },
                new PackageFramework() { TargetFramework = "netstandard10" },
                new PackageFramework() { TargetFramework = "monoandroid" }
            };

            var result = _factory.Create(packageFrameworks.ToList());

            var productNames = result.Table.Keys.ToArray();

            Assert.Equal(FrameworkProductNames.Net, productNames[0]);
            Assert.Equal(FrameworkProductNames.NetCore, productNames[1]);
            Assert.Equal(FrameworkProductNames.NetStandard, productNames[2]);
            Assert.Equal(FrameworkProductNames.NetFramework, productNames[3]);
        }

        [Fact]
        public void NoNetBasedFrameworksShouldBeOrderedAlphabetically()
        {
            var packageFrameworks = new HashSet<PackageFramework>()
            {
                new PackageFramework() { TargetFramework = "aspnet50" },
                new PackageFramework() { TargetFramework = "native" },
                new PackageFramework() { TargetFramework = "net45" },
                new PackageFramework() { TargetFramework = "net6" },
                new PackageFramework() { TargetFramework = "netcoreapp31" },
                new PackageFramework() { TargetFramework = "netstandard10" },
                new PackageFramework() { TargetFramework = "monoandroid" },
                new PackageFramework() { TargetFramework = "tizen3" },
                new PackageFramework() { TargetFramework = "uap10" },
                new PackageFramework() { TargetFramework = "wpa81" }
            };

            var result = _factory.Create(packageFrameworks.ToList());

            var productNames = result.Table.Keys.Skip(4);
            var orderedProductNames = productNames.OrderBy(x => x);

            Assert.True(productNames.SequenceEqual(orderedProductNames));
        }

        [Fact]
        public void FrameworksInTableShouldBeOnAscendingOrder()
        {
            var packageFrameworks = new HashSet<PackageFramework>()
            {
                new PackageFramework() { TargetFramework = "net6" },
                new PackageFramework() { TargetFramework = "netcoreapp31" },
                new PackageFramework() { TargetFramework = "netstandard10" },
                new PackageFramework() { TargetFramework = "net45" }
            };
            var result = _factory.Create(packageFrameworks.ToList());

            Assert.NotEmpty(result.Table);
            foreach (var row in result.Table)
            {
                var compatibleFrameworks = row.Value;
                var expectedList = compatibleFrameworks.OrderBy(cf => cf.Framework, new NuGetFrameworkSorter());

                Assert.True(expectedList.SequenceEqual(row.Value));
            }
        }

        [Fact]
        public void AllTableRowsShouldContainAtLeastOneFramework()
        {
            var packageFrameworks = new HashSet<PackageFramework>()
            {
                new PackageFramework() { TargetFramework = "net6" },
                new PackageFramework() { TargetFramework = "netcoreapp31" },
                new PackageFramework() { TargetFramework = "netstandard10" },
                new PackageFramework() { TargetFramework = "net45" }
            };
            var result = _factory.Create(packageFrameworks.ToList());

            Assert.NotEmpty(result.Table);
            foreach (var row in result.Table)
            {
                Assert.NotEmpty(row.Value);
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

        [Theory]
        [InlineData(FrameworkProductNames.Net, "net5", "net6")]
        [InlineData(FrameworkProductNames.Net, "net6", "net6-windows")]
        [InlineData(FrameworkProductNames.NetCore, "netcoreapp10", "netcoreapp21", "netcoreapp31")]
        [InlineData(FrameworkProductNames.NetStandard, "netstandard10", "netstandard10", "netstandard21")]
        [InlineData(FrameworkProductNames.NetFramework, "net11", "net45", "net472")]
        public void BadgeShouldBeTheLowestNonComputedFramework(string productFramework, string lowestFramework, params string[] frameworks)
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
            var lowestPackageFramework = new PackageFramework() { TargetFramework = lowestFramework };
            packageFrameworks.Add(lowestPackageFramework);

            var result = _factory.Create(packageFrameworks.ToList());

            NuGetFramework badgeFramework = null;
            switch (productFramework)
            {
                case FrameworkProductNames.Net: badgeFramework = result.Badges.Net; break;
                case FrameworkProductNames.NetCore: badgeFramework = result.Badges.NetCore; break;
                case FrameworkProductNames.NetStandard: badgeFramework = result.Badges.NetStandard; break;
                case FrameworkProductNames.NetFramework: badgeFramework = result.Badges.NetFramework; break;
            }

            Assert.NotNull(badgeFramework);
            Assert.Equal(lowestPackageFramework.FrameworkName, badgeFramework);
        }

        [Theory]
        [InlineData("net6")]
        [InlineData("netcoreapp31")]
        [InlineData("netstandard21")]
        [InlineData("net48")]
        public void BadgesIgnoreComputedFrameworks(string framework)
        {
            var packageFrameworks = new HashSet<PackageFramework>();
            var packageAssetFramework = new PackageFramework() { TargetFramework = framework };
            packageFrameworks.Add(packageAssetFramework);

            var result = _factory.Create(packageFrameworks.ToList());

            var badges = new List<NuGetFramework>() {
                result.Badges.Net,
                result.Badges.NetCore,
                result.Badges.NetStandard,
                result.Badges.NetFramework
            };

            var badgeFramework = badges.Single(f => f != null);
            Assert.Equal(packageAssetFramework.FrameworkName, badgeFramework);
            Assert.Equal(expected: 3, badges.Where(f => f == null).Count());

        }
    }
}
