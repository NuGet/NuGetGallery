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
        /// <summary>
        /// These frameworks are used to show badges and the first .NET products displayed on the Frameworks table.
        /// </summary>
        private readonly IReadOnlyList<string> MainNetFrameworks = new List<string> {
            FrameworkProductNames.Net,
            FrameworkProductNames.NetCore,
            FrameworkProductNames.NetStandard,
            FrameworkProductNames.NetFramework
        };
        private readonly NuGetFrameworkSorter Sorter = new NuGetFrameworkSorter();
        private readonly int NetStartingMajorVersion = 5;

        private readonly IFrameworkCompatibilityService _compatibilityService;

        public PackageFrameworkCompatibilityFactory(IFrameworkCompatibilityService compatibilityService)
        {
            _compatibilityService = compatibilityService ?? throw new ArgumentNullException();
        }

        public PackageFrameworkCompatibility Create(ICollection<PackageFramework> packageFrameworks)
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
            var badges = CreateFrameworkCompatibilityBadges(table);

            return new PackageFrameworkCompatibility
            {
                Badges = badges,
                Table = table
            };
        }

        private IReadOnlyDictionary<string, IReadOnlyCollection<PackageFrameworkCompatibilityTableData>> CreateFrameworkCompatibilityTable(ICollection<NuGetFramework> filteredPackageFrameworks)
        {
            var compatibleFrameworks = _compatibilityService.GetCompatibleFrameworks(filteredPackageFrameworks);

            var table = new Dictionary<string, SortedSet<PackageFrameworkCompatibilityTableData>>();

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

        private IReadOnlyDictionary<string, IReadOnlyCollection<PackageFrameworkCompatibilityTableData>> OrderDictionaryKeys(Dictionary<string, SortedSet<PackageFrameworkCompatibilityTableData>> table)
        {
            var orderedTable = new Dictionary<string, IReadOnlyCollection<PackageFrameworkCompatibilityTableData>>();

            AddOrderedKey(table, orderedTable, FrameworkProductNames.Net);
            AddOrderedKey(table, orderedTable, FrameworkProductNames.NetCore);
            AddOrderedKey(table, orderedTable, FrameworkProductNames.NetStandard);
            AddOrderedKey(table, orderedTable, FrameworkProductNames.NetFramework);

            foreach (var orderedKey in table.Keys.OrderBy(k => k))
            {
                if (!MainNetFrameworks.Contains(orderedKey))
                {
                    table.TryGetValue(orderedKey, out var compatibleFrameworks);
                    orderedTable.Add(orderedKey, compatibleFrameworks);
                }
            }

            return orderedTable;
        }

        private void AddOrderedKey(Dictionary<string, SortedSet<PackageFrameworkCompatibilityTableData>> table, Dictionary<string, IReadOnlyCollection<PackageFrameworkCompatibilityTableData>> orderedTable, string framework)
        {
            if (table.TryGetValue(framework, out var compatibleFrameworks))
            {
                orderedTable.Add(framework, compatibleFrameworks);
            }
        }

        private IReadOnlyList<PackageFrameworkCompatibilityBadge> CreateFrameworkCompatibilityBadges(IReadOnlyDictionary<string, IReadOnlyCollection<PackageFrameworkCompatibilityTableData>> table)
        {
            var badges = new List<PackageFrameworkCompatibilityBadge>();

            foreach (var frameworkName in MainNetFrameworks)
            {
                var badge = GetBadgeFramework(table, frameworkName);
                if (badge != null)
                {
                    badges.Add(badge);
                }
            }

            return badges;
        }

        private PackageFrameworkCompatibilityBadge GetBadgeFramework(IReadOnlyDictionary<string, IReadOnlyCollection<PackageFrameworkCompatibilityTableData>> table, string productName)
        {
            if (table.TryGetValue(productName, out var data))
            {
                var frameworks = data
                    .Where(d => !d.IsComputed)
                    .Select(d => d.Framework);

                var lowestFramework = frameworks.FirstOrDefault();

                if (lowestFramework != null)
                {
                    return new PackageFrameworkCompatibilityBadge
                    {
                        FrameworkProductName = productName,
                        Framework = lowestFramework,
                        HasHigherVersions = frameworks.Count() > 1
                    };
                }
            }

            return null;
        }
    }
}
