// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using NuGet.Frameworks;
using NuGet.Services.Entities;

namespace NuGetGallery.Frameworks
{
    public class PackageFrameworkCompatibilityFactory : IPackageFrameworkCompatibilityFactory
    {
        private readonly ISet<Version> WindowsStoreNetCoreVersions = new HashSet<Version> { FrameworkConstants.EmptyVersion, Version.Parse("4.5.0.0"), Version.Parse("4.5.1.0") };
        private readonly ISet<Version> WindowsStoreWindowsVersions = new HashSet<Version> { FrameworkConstants.EmptyVersion, Version.Parse("8.0.0.0"), Version.Parse("8.1.0.0") };
        private readonly ISet<string> TableFirstFrameworks = new HashSet<string> {
            FrameworkProductNames.Net,
            FrameworkProductNames.NetCore,
            FrameworkProductNames.NetStandard,
            FrameworkProductNames.NetFramework
        };
        private readonly NuGetFrameworkSorter Sorter = NuGetFrameworkSorter.Instance;
        private readonly int NetStartingMajorVersion = 5;

        public PackageFrameworkCompatibility Create(ICollection<PackageFramework> packageFrameworks, string packageId, string packageVersion, bool includeComputedBadges = false)
        {
            if (packageFrameworks == null)
            {
                throw new ArgumentNullException(nameof(packageFrameworks));
            }

            var filteredPackageFrameworks = packageFrameworks
                .Select(pf => pf.FrameworkName)
                .Where(f => !f.IsUnsupported && !f.IsPCL && !f.IsAny)
                .ToHashSet();

            var table = CreateFrameworkCompatibilityTable(filteredPackageFrameworks);
            var badges = CreateFrameworkCompatibilityBadges(table, packageId, packageVersion, includeComputedBadges);

            return new PackageFrameworkCompatibility
            {
                Badges = badges,
                Table = table
            };
        }

        private IReadOnlyDictionary<string, IReadOnlyCollection<PackageFrameworkCompatibilityData>> CreateFrameworkCompatibilityTable(ICollection<NuGetFramework> filteredPackageFrameworks)
        {
            var compatibleFrameworks = FrameworkCompatibilityService.GetCompatibleFrameworks(filteredPackageFrameworks);

            var table = new Dictionary<string, SortedSet<PackageFrameworkCompatibilityData>>();

            foreach (var compatibleFramework in compatibleFrameworks)
            {
                var productName = ResolveFrameworkProductName(compatibleFramework);
                var data = new PackageFrameworkCompatibilityData
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
                    var newCompatibleFrameworks = new SortedSet<PackageFrameworkCompatibilityData>
                        (Comparer<PackageFrameworkCompatibilityData>.Create((a, b) => Sorter.Compare(a.Framework, b.Framework)));

                    newCompatibleFrameworks.Add(data);
                    table.Add(productName, newCompatibleFrameworks);
                }
            }

            return OrderDictionaryKeys(table);
        }

        private string ResolveFrameworkProductName(NuGetFramework framework)
        {
            // See: https://docs.microsoft.com/en-us/dotnet/standard/frameworks#supported-target-frameworks
            switch (framework.Framework)
            {
                case FrameworkConstants.FrameworkIdentifiers.NetCoreApp:
                    return framework.Version.Major >= NetStartingMajorVersion ? FrameworkProductNames.Net : FrameworkProductNames.NetCore;
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

        private IReadOnlyDictionary<string, IReadOnlyCollection<PackageFrameworkCompatibilityData>> OrderDictionaryKeys(Dictionary<string, SortedSet<PackageFrameworkCompatibilityData>> table)
        {
            var orderedTable = new Dictionary<string, IReadOnlyCollection<PackageFrameworkCompatibilityData>>();

            AddOrderedKey(table, orderedTable, FrameworkProductNames.Net);
            AddOrderedKey(table, orderedTable, FrameworkProductNames.NetCore);
            AddOrderedKey(table, orderedTable, FrameworkProductNames.NetStandard);
            AddOrderedKey(table, orderedTable, FrameworkProductNames.NetFramework);

            foreach (var orderedKey in table.Keys.OrderBy(k => k))
            {
                if (!TableFirstFrameworks.Contains(orderedKey))
                {
                    table.TryGetValue(orderedKey, out var compatibleFrameworks);
                    orderedTable.Add(orderedKey, compatibleFrameworks);
                }
            }

            return orderedTable;
        }

        private void AddOrderedKey(Dictionary<string, SortedSet<PackageFrameworkCompatibilityData>> table, Dictionary<string, IReadOnlyCollection<PackageFrameworkCompatibilityData>> orderedTable, string framework)
        {
            if (table.TryGetValue(framework, out var compatibleFrameworks))
            {
                orderedTable.Add(framework, compatibleFrameworks);
            }
        }

        private PackageFrameworkCompatibilityBadges CreateFrameworkCompatibilityBadges(
            IReadOnlyDictionary<string, IReadOnlyCollection<PackageFrameworkCompatibilityData>> table,
            string packageId,
            string packageVersion,
            bool includeComputed = false)
        {
            var net = GetBadgeFramework(table, FrameworkProductNames.Net, includeComputed);
            var netCore = GetBadgeFramework(table, FrameworkProductNames.NetCore, includeComputed);
            var netStandard = GetBadgeFramework(table, FrameworkProductNames.NetStandard, includeComputed);
            var netFramework = GetBadgeFramework(table, FrameworkProductNames.NetFramework, includeComputed);

            return new PackageFrameworkCompatibilityBadges
            {
                PackageId = packageId,
                PackageVersion = packageVersion,
                Net = net,
                NetCore = netCore,
                NetStandard = netStandard,
                NetFramework = netFramework
            };
        }

        private PackageFrameworkCompatibilityData GetBadgeFramework(IReadOnlyDictionary<string, IReadOnlyCollection<PackageFrameworkCompatibilityData>> table, string productName, bool includeComputed = false)
        {
            if (table.TryGetValue(productName, out var data))
            {
                return data
                    .FirstOrDefault(d => includeComputed || !d.IsComputed);
            }

            return null;
        }
    }
}
