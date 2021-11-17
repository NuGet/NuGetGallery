// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using NuGet.Frameworks;
using NuGet.Services.Entities;

namespace NuGetGallery.Frameworks
{
    public class PackageFrameworkCompatibilityFactory
    {
        private readonly ISet<Version> WindowsStoreNetCoreVersions = new HashSet<Version> { FrameworkConstants.EmptyVersion, Version.Parse("4.5.0.0"), Version.Parse("4.5.1.0") };
        private readonly ISet<Version> WindowsStoreWindowsVersions = new HashSet<Version> { FrameworkConstants.EmptyVersion, Version.Parse("8.0.0.0"), Version.Parse("8.1.0.0") };
        private readonly NuGetFrameworkSorter Sorter = new NuGetFrameworkSorter();

        private readonly IFrameworkCompatibilityService _service;

        public PackageFrameworkCompatibilityFactory(IFrameworkCompatibilityService service)
        {
            _service = service ?? throw new ArgumentNullException();
        }

        public PackageFrameworkCompatibility Create(ICollection<PackageFramework> packageFrameworks)
        {
            if (packageFrameworks == null)
            {
                throw new ArgumentNullException(nameof(packageFrameworks));
            }

            var filteredPackageFrameworks = packageFrameworks
                .Select(pf => pf.FrameworkName)
                .Where(f => !f.IsUnsupported && !f.IsPCL)
                .ToHashSet();

            var table = CreateFrameworkCompatibilityTable(filteredPackageFrameworks);
            var badges = CreateFrameworkCompatibilityBadges(table);

            return new PackageFrameworkCompatibility
            {
                Badges = badges,
                Table = table
            };
        }

        private IReadOnlyDictionary<string, ICollection<PackageFrameworkCompatibilityTableData>> CreateFrameworkCompatibilityTable(ICollection<NuGetFramework> filteredPackageFrameworks)
        {
            var compatibleFrameworks = _service.GetCompatibleFrameworks(filteredPackageFrameworks);

            var table = new Dictionary<string, ICollection<PackageFrameworkCompatibilityTableData>>();

            foreach (var compatibleFramework in compatibleFrameworks)
            {
                var productName = ResolveFrameworkProductName(compatibleFramework);
                var data = new PackageFrameworkCompatibilityTableData
                {
                    Framework = compatibleFramework,
                    IsComputed = !filteredPackageFrameworks.Contains(compatibleFramework)
                };

                if (table.TryGetValue(productName, out var allCompatibleFrameworks))
                {
                    allCompatibleFrameworks.Add(data);
                }
                else
                {
                    var newCompatibleFrameworks = new SortedSet<PackageFrameworkCompatibilityTableData>
                        (Comparer<PackageFrameworkCompatibilityTableData>.Create((a, b) => Sorter.Compare(a.Framework, b.Framework)));

                    newCompatibleFrameworks.Add(data);
                    table.Add(productName, newCompatibleFrameworks);
                }
            }

            return table;
        }

        private string ResolveFrameworkProductName(NuGetFramework framework)
        {
            // See: https://docs.microsoft.com/en-us/dotnet/standard/frameworks#supported-target-frameworks
            switch (framework.Framework)
            {
                case FrameworkConstants.FrameworkIdentifiers.NetCoreApp:
                    return framework.Version.Major >= 5 ? FrameworkProductNames.Net : FrameworkProductNames.NetCore;
                case FrameworkConstants.FrameworkIdentifiers.NetStandard:
                case FrameworkConstants.FrameworkIdentifiers.NetStandardApp:
                    return FrameworkProductNames.NetStandard;
                case FrameworkConstants.FrameworkIdentifiers.Net:
                    return FrameworkProductNames.NetFramework;
                case FrameworkConstants.FrameworkIdentifiers.NetMicro:
                    return FrameworkProductNames.NetMicroFramework;
                case FrameworkConstants.FrameworkIdentifiers.WindowsPhoneApp:
                case FrameworkConstants.FrameworkIdentifiers.WindowsPhone:
                    return FrameworkProductNames.WindowsPhone;
                case FrameworkConstants.FrameworkIdentifiers.NetCore:
                    if (framework.Version == FrameworkConstants.Version5)
                    {
                        return FrameworkProductNames.UWP;
                    }

                    if (WindowsStoreNetCoreVersions.Contains(framework.Version))
                    {
                        return FrameworkProductNames.WindowsStore;
                    }

                    return FrameworkConstants.FrameworkIdentifiers.NetCore;
                case FrameworkConstants.FrameworkIdentifiers.Windows:
                    if (WindowsStoreWindowsVersions.Contains(framework.Version))
                    {
                        return FrameworkProductNames.WindowsStore;
                    }
                    return FrameworkConstants.FrameworkIdentifiers.Windows;
                case FrameworkConstants.FrameworkIdentifiers.UAP:
                    return FrameworkProductNames.UWP;
            }

            return framework.Framework;
        }

        private PackageFrameworkCompatibilityBadges CreateFrameworkCompatibilityBadges(IReadOnlyDictionary<string, ICollection<PackageFrameworkCompatibilityTableData>> table)
        {
            var net = GetBadgeFramework(table, FrameworkProductNames.Net);
            var netCore = GetBadgeFramework(table, FrameworkProductNames.NetCore);
            var netStandard = GetBadgeFramework(table, FrameworkProductNames.NetStandard);
            var netFramework = GetBadgeFramework(table, FrameworkProductNames.NetFramework);

            return new PackageFrameworkCompatibilityBadges
            {
                Net = net,
                NetCore = netCore,
                NetStandard = netStandard,
                NetFramework = netFramework
            };
        }

        private NuGetFramework GetBadgeFramework(IReadOnlyDictionary<string, ICollection<PackageFrameworkCompatibilityTableData>> table, string productName)
        {
            if (table.TryGetValue(productName, out var data))
            {
                return data
                    .Where(d => !d.IsComputed)
                    .Select(d => d.Framework)
                    .FirstOrDefault();
            }

            return null;
        }
    }
}
