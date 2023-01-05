// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Web.Mvc;
using NuGet.Services.Entities;

namespace NuGetGallery
{
    public class PackageListViewModel
    {
        public PackageListViewModel(
            IReadOnlyCollection<ListPackageItemViewModel> packageViewModels,
            DateTime? indexTimestampUtc,
            string searchTerm,
            int totalCount,
            int pageIndex,
            int pageSize,
            UrlHelper url,
            bool includePrerelease,
            bool isPreviewSearch,
            string frameworks,
            string tfms,
            string packageType,
            string sortBy)
        {
            PageIndex = pageIndex;
            IndexTimestampUtc = indexTimestampUtc;
            PageSize = pageSize;
            TotalCount = totalCount;
            SearchTerm = searchTerm;
            int pageCount = (TotalCount + PageSize - 1) / PageSize;

            var pager = new PreviousNextPagerViewModel<ListPackageItemViewModel>(
                packageViewModels,
                PageIndex,
                pageCount,
                page => url.PackageList(page, searchTerm, includePrerelease, frameworks, tfms, packageType, sortBy));
            Items = pager.Items;
            Pager = pager;
            IncludePrerelease = includePrerelease;
            IsPreviewSearch = isPreviewSearch;
            Frameworks = frameworks;
            Tfms = tfms;
            PackageType = packageType;
            SortBy = sortBy;

            InitializeFrameworkFilters();
        }

        public int FirstResultIndex => 1 + (PageIndex * PageSize);
        public int LastResultIndex => FirstResultIndex + Items.Count() - 1;

        public IReadOnlyCollection<ListPackageItemViewModel> Items { get; }

        public IPreviousNextPager Pager { get; }

        public int TotalCount { get; }

        public string SearchTerm { get;  }

        public int PageIndex { get; }

        public int PageSize { get; }

        public DateTime? IndexTimestampUtc { get; }

        public bool IncludePrerelease { get; }

        public bool IsPreviewSearch { get; }

        public string Frameworks { get; set; }

        public string Tfms { get; set; }

        public string PackageType { get; set; }

        public string SortBy { get; set; }

        public bool ShouldDisplayAdvancedSearchPanel { get; set; }

        public bool IsAdvancedSearchFlightEnabled { get; set; }

        public Dictionary<string, FrameworkFilterGroup> FrameworkFilters { get; set; }

        /// <summary>
        /// Each Framework Filter Group represents one of the four Framework generations
        /// represented in the Search Filters.
        /// </summary>
        public class FrameworkFilterGroup
        {
            public FrameworkFilterGroup(
                string shortName,
                string displayName,
                List<string> tfms)
            {
                ShortName = shortName;
                DisplayName = displayName;
                Tfms = tfms;
            }

            public string ShortName { get; set; }
            public string DisplayName { get; set; }
            public List<string> Tfms { get; set; }
        }

        public void InitializeFrameworkFilters()
        {
            FrameworkFilters = new Dictionary<string, FrameworkFilterGroup>();

            FrameworkFilters.Add(AssetFrameworkHelper.FrameworkGenerationIdentifiers.Net,
                new FrameworkFilterGroup(
                    AssetFrameworkHelper.FrameworkGenerationIdentifiers.Net,
                    AssetFrameworkHelper.FrameworkGenerationDisplayNames.Net,
                    new List<string> { "net5.0",
                                       "net6.0",
                                       "net7.0",
                                       "net8.0" }
                    ));

            FrameworkFilters.Add(AssetFrameworkHelper.FrameworkGenerationIdentifiers.NetCoreApp,
                new FrameworkFilterGroup(
                    AssetFrameworkHelper.FrameworkGenerationIdentifiers.NetCoreApp,
                    AssetFrameworkHelper.FrameworkGenerationDisplayNames.NetCoreApp,
                    new List<string> { "netcoreapp1.0",
                                       "netcoreapp1.1",
                                       "netcoreapp2.0",
                                       "netcoreapp2.1",
                                       "netcoreapp2.2",
                                       "netcoreapp3.0",
                                       "netcoreapp3.1", }
                    ));

            FrameworkFilters.Add(AssetFrameworkHelper.FrameworkGenerationIdentifiers.NetStandard,
                new FrameworkFilterGroup(
                    AssetFrameworkHelper.FrameworkGenerationIdentifiers.NetStandard,
                    AssetFrameworkHelper.FrameworkGenerationDisplayNames.NetStandard,
                    new List<string> { "netstandard1.0",
                                       "netstandard1.1",
                                       "netstandard1.2",
                                       "netstandard1.3",
                                       "netstandard1.4",
                                       "netstandard1.5",
                                       "netstandard1.6",
                                       "netstandard2.0",
                                       "netstandard2.1" }
                    ));

            FrameworkFilters.Add(AssetFrameworkHelper.FrameworkGenerationIdentifiers.NetFramework,
                new FrameworkFilterGroup(
                    AssetFrameworkHelper.FrameworkGenerationIdentifiers.NetFramework,
                    AssetFrameworkHelper.FrameworkGenerationDisplayNames.NetFramework,
                    new List<string> { "net20",
                                       "net30",
                                       "net35",
                                       "net40",
                                       "net45",
                                       "net451",
                                       "net452",
                                       "net46",
                                       "net461",
                                       "net462",
                                       "net47",
                                       "net471",
                                       "net472",
                                       "net48",
                                       "net481" }
                    ));
        }
    }
}