// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using Azure.Search.Documents.Indexes;
using Azure.Search.Documents.Models;
using Moq;
using Xunit;

namespace NuGet.Services.AzureSearch.SearchService
{
    public class SearchParametersBuilderFacts
    {
        public class GetSearchFilters : BaseFacts
        {
            [Theory]
            [MemberData(nameof(AllSearchFilters))]
            public void SearchFilters(bool includePrerelease, bool includeSemVer2, SearchFilters filter)
            {
                var request = new SearchRequest
                {
                    IncludePrerelease = includePrerelease,
                    IncludeSemVer2 = includeSemVer2,
                };

                var actual = _target.GetSearchFilters(request);

                Assert.Equal(filter, actual);
            }
        }

        public class LastCommitTimestamp : BaseFacts
        {
            [Fact]
            public void Defaults()
            {
                var output = _target.LastCommitTimestamp();

                Assert.Equal(SearchQueryType.Full, output.QueryType);
                Assert.Null(output.IncludeTotalCount);
                Assert.Equal(new[] { "lastCommitTimestamp desc" }, output.OrderBy.ToArray());
                Assert.Equal(0, output.Skip);
                Assert.Equal(1, output.Size);
                Assert.Null(output.Filter);
            }
        }

        public class V2Search : BaseFacts
        {
            [Fact]
            public void Defaults()
            {
                var request = new V2SearchRequest();

                var output = _target.V2Search(request, isDefaultSearch: true);

                Assert.Equal(SearchQueryType.Full, output.QueryType);
                Assert.True(output.IncludeTotalCount);
                Assert.Equal(DefaultOrderBy, output.OrderBy.ToArray());
                Assert.Equal(0, output.Skip);
                Assert.Equal(0, output.Size);
                Assert.Equal("searchFilters eq 'Default' and (isExcludedByDefault eq false or isExcludedByDefault eq null)", output.Filter);
            }
            
            [Theory]
            [MemberData(nameof(ValidPackageTypes))]
            public void PackageTypeFiltering(string packageType)
            {
                var request = new V2SearchRequest
                {
                    PackageType = packageType,
                };

                var output = _target.V2Search(request, It.IsAny<bool>());

                Assert.Equal($"searchFilters eq 'Default' and filterablePackageTypes/any(p: p eq '{packageType.ToLowerInvariant()}')", output.Filter);
            }

            [Fact]
            public void InvalidPackageType()
            {
                var request = new V2SearchRequest
                {
                    PackageType = "something's-weird",
                };

                var output = _target.V2Search(request, It.IsAny<bool>());

                Assert.Equal("searchFilters eq 'Default'", output.Filter);
            }

            [Fact]
            public void CountOnly()
            {
                var request = new V2SearchRequest
                {
                    CountOnly = true,
                    Skip = 10,
                    Take = 30,
                    SortBy = V2SortBy.SortableTitleAsc,
                };

                var output = _target.V2Search(request, It.IsAny<bool>());

                Assert.Equal(SearchQueryType.Full, output.QueryType);
                Assert.True(output.IncludeTotalCount);
                Assert.Empty(output.OrderBy);
                Assert.Equal(0, output.Skip);
                Assert.Equal(0, output.Size);
            }

            [Fact]
            public void Paging()
            {
                var request = new V2SearchRequest
                {
                    Skip = 10,
                    Take = 30,
                };

                var output = _target.V2Search(request, It.IsAny<bool>());

                Assert.Equal(10, output.Skip);
                Assert.Equal(30, output.Size);
            }

            [Fact]
            public void NegativeSkip()
            {
                var request = new V2SearchRequest
                {
                    Skip = -10,
                };

                var output = _target.V2Search(request, It.IsAny<bool>());

                Assert.Equal(0, output.Skip);
            }

            [Fact]
            public void NegativeTake()
            {
                var request = new V2SearchRequest
                {
                    Take = -20,
                };

                var output = _target.V2Search(request, It.IsAny<bool>());

                Assert.Equal(20, output.Size);
            }

            [Fact]
            public void TooLargeTake()
            {
                var request = new V2SearchRequest
                {
                    Take = 1001,
                };

                var output = _target.V2Search(request, It.IsAny<bool>());

                Assert.Equal(20, output.Size);
            }

            [Theory]
            [InlineData(false, false, "semVerLevel ne 2")]
            [InlineData(true, false, "semVerLevel ne 2")]
            [InlineData(false, true, null)]
            [InlineData(true, true, null)]
            public void IgnoreFilter(bool includePrerelease, bool includeSemVer2, string filter)
            {
                var request = new V2SearchRequest
                {
                    IgnoreFilter = true,
                    IncludePrerelease = includePrerelease,
                    IncludeSemVer2 = includeSemVer2,
                };

                var output = _target.V2Search(request, It.IsAny<bool>());

                Assert.Equal(filter, output.Filter);
            }

            [Theory]
            [MemberData(nameof(AllV2SortBy))]
            public void SortBy(V2SortBy v2SortBy)
            {
                var request = new V2SearchRequest
                {
                    SortBy = v2SortBy,
                };
                var expectedOrderBy = V2SortByToOrderBy[v2SortBy];

                var output = _target.V2Search(request, It.IsAny<bool>());

                Assert.NotNull(output.OrderBy);
                Assert.Equal(expectedOrderBy, output.OrderBy.ToArray());
            }

            [Theory]
            [MemberData(nameof(AllV2SortBy))]
            public void AllSortByFieldsAreSortable(V2SortBy v2SortBy)
            {
                var metadataProperties = typeof(BaseMetadataDocument)
                    .GetProperties()
                    .Union(typeof(SearchDocument.Full).GetProperties()) // Properties can also be in a SearchDocument (e.g: TotalDownloadCount)
                    .GroupBy(x => x.Name, StringComparer.OrdinalIgnoreCase)
                    .Select(g => g.First())
                    .ToDictionary(x => x.Name, x => x, StringComparer.OrdinalIgnoreCase);

                var expectedOrderBy = V2SortByToOrderBy[v2SortBy];

                foreach (var clause in expectedOrderBy)
                {
                    var pieces = clause.Split(new[] { ' ' }, 2);
                    Assert.Equal(2, pieces.Length);
                    Assert.Contains(pieces[1], new[] { "asc", "desc" });

                    // This is a magic property name that refers to the document's score, not a particular property.
                    if (pieces[0] == "search.score()")
                    {
                        continue;
                    }

                    Assert.Contains(pieces[0], metadataProperties.Keys, StringComparer.OrdinalIgnoreCase);
                    var property = metadataProperties[pieces[0]];
                    var attribute = property
                        .CustomAttributes
                        .FirstOrDefault(x => x.AttributeType == typeof(SimpleFieldAttribute));
                    Assert.NotNull(attribute);
                    var argument = attribute.NamedArguments.First(x => x.MemberName == nameof(SimpleFieldAttribute.IsSortable));
                    Assert.Equal(true, argument.TypedValue.Value);
                }
            }

            [Theory]
            [MemberData(nameof(AllSearchFiltersExpressions))]
            public void SearchFilters(bool includePrerelease, bool includeSemVer2, string filter)
            {
                var request = new V2SearchRequest
                {
                    IncludePrerelease = includePrerelease,
                    IncludeSemVer2 = includeSemVer2,
                    Query = "js"
                };

                var output = _target.V2Search(request, It.IsAny<bool>());

                Assert.Equal(filter, output.Filter);
            }

            [Theory]
            [MemberData(nameof(FrameworkAndTfmCases))]
            [MemberData(nameof(ComputedFrameworkCases))]
            public void FrameworkAndTfmFiltering(List<string> frameworks, List<string> tfms, string expectedFilterString, bool includeComputedFrameworks = false)
            {
                // arrange
                var request = new V2SearchRequest
                {
                    Frameworks = frameworks,
                    Tfms = tfms,
                    IncludeComputedFrameworks = includeComputedFrameworks,
                };

                // act
                var output = _target.V2Search(request, isDefaultSearch: false);

                // assert
                Assert.Equal($"searchFilters eq 'Default' and {expectedFilterString}", output.Filter);
            }

            [Theory]
            [MemberData(nameof(FrameworkFilterModeCases))]
            public void FilterStringShouldMatchFrameworkFilterMode(List<string> frameworks, List<string> tfms, string frameworkFilterMode, string expectedFilterString)
            {
                // arrange
                var request = new V2SearchRequest
                {
                    Frameworks = frameworks,
                    Tfms = tfms,
                    IncludeComputedFrameworks = false,
                    FrameworkFilterMode = ParameterUtilities.ParseV2FrameworkFilterMode(frameworkFilterMode)
                };

                // act
                var output = _target.V2Search(request, isDefaultSearch: false);

                // assert
                Assert.Equal($"searchFilters eq 'Default' and {expectedFilterString}", output.Filter);
            }
        }

        public class V3Search : BaseFacts
        {
            [Fact]
            public void Defaults()
            {
                var request = new V3SearchRequest();

                var output = _target.V3Search(request, isDefaultSearch: true);

                Assert.Equal(SearchQueryType.Full, output.QueryType);
                Assert.True(output.IncludeTotalCount);
                Assert.Equal(DefaultOrderBy, output.OrderBy.ToArray());
                Assert.Equal(0, output.Skip);
                Assert.Equal(0, output.Size);
                Assert.Equal("searchFilters eq 'Default' and (isExcludedByDefault eq false or isExcludedByDefault eq null)", output.Filter);
            }

            [Theory]
            [MemberData(nameof(ValidPackageTypes))]
            public void PackageTypeFiltering(string packageType)
            {
                var request = new V3SearchRequest
                {
                    PackageType = packageType,
                };

                var output = _target.V3Search(request, It.IsAny<bool>());

                Assert.Equal($"searchFilters eq 'Default' and filterablePackageTypes/any(p: p eq '{packageType.ToLowerInvariant()}')", output.Filter);
            }

            [Fact]
            public void InvalidPackageType()
            {
                var request = new V3SearchRequest
                {
                    PackageType = "something's-weird",
                };

                var output = _target.V3Search(request, It.IsAny<bool>());

                Assert.Equal("searchFilters eq 'Default'", output.Filter);
            }

            [Fact]
            public void Paging()
            {
                var request = new V3SearchRequest
                {
                    Skip = 10,
                    Take = 30,
                };

                var output = _target.V3Search(request, It.IsAny<bool>());

                Assert.Equal(10, output.Skip);
                Assert.Equal(30, output.Size);
            }

            [Fact]
            public void NegativeSkip()
            {
                var request = new V3SearchRequest
                {
                    Skip = -10,
                };

                var output = _target.V3Search(request, It.IsAny<bool>());

                Assert.Equal(0, output.Skip);
            }

            [Fact]
            public void NegativeTake()
            {
                var request = new V3SearchRequest
                {
                    Take = -20,
                };

                var output = _target.V3Search(request, It.IsAny<bool>());

                Assert.Equal(20, output.Size);
            }

            [Fact]
            public void TooLargeTake()
            {
                var request = new V3SearchRequest
                {
                    Take = 1001,
                };

                var output = _target.V3Search(request, It.IsAny<bool>());

                Assert.Equal(20, output.Size);
            }

            [Theory]
            [MemberData(nameof(AllSearchFiltersExpressions))]
            public void SearchFilters(bool includePrerelease, bool includeSemVer2, string filter)
            {
                var request = new V3SearchRequest
                {
                    IncludePrerelease = includePrerelease,
                    IncludeSemVer2 = includeSemVer2,
                    Query = "js"
                };

                var output = _target.V3Search(request, It.IsAny<bool>());

                Assert.Equal(filter, output.Filter);
            }
        }

        public class Autocomplete : BaseFacts
        {
            [Fact]
            public void PackageIdsDefaults()
            {
                var request = new AutocompleteRequest();
                request.Type = AutocompleteRequestType.PackageIds;

                var output = _target.Autocomplete(request, isDefaultSearch: true);

                Assert.Equal(SearchQueryType.Full, output.QueryType);
                Assert.True(output.IncludeTotalCount);
                Assert.Equal(DefaultOrderBy, output.OrderBy.ToArray());
                Assert.Equal(0, output.Skip);
                Assert.Equal(0, output.Size);
                Assert.Equal("searchFilters eq 'Default' and (isExcludedByDefault eq false or isExcludedByDefault eq null)", output.Filter);
                Assert.Single(output.Select);
                Assert.Equal(IndexFields.PackageId, output.Select[0]);
            }

            [Fact]
            public void PackageVersionsDefaults()
            {
                var request = new AutocompleteRequest();
                request.Type = AutocompleteRequestType.PackageVersions;

                var output = _target.Autocomplete(request, isDefaultSearch: true);

                Assert.Equal(SearchQueryType.Full, output.QueryType);
                Assert.True(output.IncludeTotalCount);
                Assert.Equal(DefaultOrderBy, output.OrderBy.ToArray());
                Assert.Equal(0, output.Skip);
                Assert.Equal(1, output.Size);
                Assert.Equal("searchFilters eq 'Default' and (isExcludedByDefault eq false or isExcludedByDefault eq null)", output.Filter);
                Assert.Single(output.Select);
                Assert.Equal(IndexFields.Search.Versions, output.Select[0]);
            }

            [Fact]
            public void Paging()
            {
                var request = new AutocompleteRequest
                {
                    Skip = 10,
                    Take = 30,
                    Type = AutocompleteRequestType.PackageIds,
                };

                var output = _target.Autocomplete(request, It.IsAny<bool>());

                Assert.Equal(10, output.Skip);
                Assert.Equal(30, output.Size);
            }

            [Fact]
            public void PackageVersionsPaging()
            {
                var request = new AutocompleteRequest
                {
                    Skip = 10,
                    Take = 30,
                    Type = AutocompleteRequestType.PackageVersions,
                };

                var output = _target.Autocomplete(request, It.IsAny<bool>());

                Assert.Equal(0, output.Skip);
                Assert.Equal(1, output.Size);
            }

            [Fact]
            public void NegativeSkip()
            {
                var request = new AutocompleteRequest
                {
                    Skip = -10,
                    Type = AutocompleteRequestType.PackageIds,
                };

                var output = _target.Autocomplete(request, It.IsAny<bool>());

                Assert.Equal(0, output.Skip);
            }

            [Fact]
            public void NegativeTake()
            {
                var request = new AutocompleteRequest
                {
                    Take = -20,
                    Type = AutocompleteRequestType.PackageIds,
                };

                var output = _target.Autocomplete(request, It.IsAny<bool>());

                Assert.Equal(20, output.Size);
            }

            [Fact]
            public void TooLargeTake()
            {
                var request = new AutocompleteRequest
                {
                    Type = AutocompleteRequestType.PackageIds,
                    Take = 1001,
                };

                var output = _target.Autocomplete(request, It.IsAny<bool>());

                Assert.Equal(20, output.Size);
            }

            [Theory]
            [MemberData(nameof(AllSearchFiltersExpressions))]
            public void SearchFilters(bool includePrerelease, bool includeSemVer2, string filter)
            {
                var request = new AutocompleteRequest
                {
                    IncludePrerelease = includePrerelease,
                    IncludeSemVer2 = includeSemVer2,
                    Query = "js"
                };

                var output = _target.Autocomplete(request, It.IsAny<bool>());

                Assert.Equal(filter, output.Filter);
            }

            [Theory]
            [MemberData(nameof(ValidPackageTypes))]
            public void PackageTypeFiltering(string packageType)
            {
                var request = new AutocompleteRequest
                {
                    PackageType = packageType,
                };

                var output = _target.Autocomplete(request, It.IsAny<bool>());

                Assert.Equal($"searchFilters eq 'Default' and filterablePackageTypes/any(p: p eq '{packageType.ToLowerInvariant()}')", output.Filter);
            }

            [Fact]
            public void InvalidPackageType()
            {
                var request = new AutocompleteRequest
                {
                    PackageType = "something's-weird",
                };

                var output = _target.Autocomplete(request, It.IsAny<bool>());

                Assert.Equal("searchFilters eq 'Default'", output.Filter);
            }
        }

        public abstract class BaseFacts
        {
            protected readonly SearchParametersBuilder _target;

            public static string[] DefaultOrderBy => new[] { "search.score() desc", "created desc" };

            public static IReadOnlyDictionary<V2SortBy, string[]> V2SortByToOrderBy => new Dictionary<V2SortBy, string[]>
            {
                { V2SortBy.LastEditedDesc, new[] { "lastEdited desc", "created desc" } },
                { V2SortBy.Popularity, DefaultOrderBy },
                { V2SortBy.PublishedDesc, new[] { "published desc", "created desc" } },
                { V2SortBy.SortableTitleAsc, new[] { "sortableTitle asc", "created asc" } },
                { V2SortBy.SortableTitleDesc, new[] { "sortableTitle desc", "created desc" } },
                { V2SortBy.CreatedAsc, new[] { "created asc" } },
                { V2SortBy.CreatedDesc, new[] { "created desc" } },
                { V2SortBy.TotalDownloadsAsc, new[] { "totalDownloadCount asc", "created asc"} },
                { V2SortBy.TotalDownloadsDesc, new[] { "totalDownloadCount desc", "created desc"} },
            };

            public static IEnumerable<object[]> AllSearchFilters => new[]
            {
                new object[] { false, false, SearchFilters.Default },
                new object[] { true, false, SearchFilters.IncludePrerelease },
                new object[] { false, true, SearchFilters.IncludeSemVer2 },
                new object[] { true, true, SearchFilters.IncludePrereleaseAndSemVer2 },
            };

            public static IEnumerable<object[]> AllSearchFiltersExpressions => AllSearchFilters
                .Select(x => new[] { x[0], x[1], $"searchFilters eq '{x[2]}'" });

            public static IEnumerable<object[]> AllV2SortBy => Enum
                .GetValues(typeof(V2SortBy))
                .Cast<V2SortBy>()
                .Select(x => new object[] { x });


            public static IEnumerable<object[]> ValidPackageTypes => new[]
            {
                new object[] { "Dependency" },
                new object[] { "DotnetTool" },
                new object[] { "Template" },
                new object[] { "PackageType.With.Dots" },
                new object[] { "PackageType-With-Hyphens" },
                new object[] { "PackageType_With_Underscores" },
            };

            public static IEnumerable<object[]> FrameworkAndTfmCases => new[]
            {
                new object[] {  new List<string> {"netstandard"},
                                new List<string> {"net472"},
                                "((frameworks/any(f: f eq 'netstandard')) and (tfms/any(f: f eq 'net472')))" },
                new object[] {  new List<string> {"netcoreapp"},
                                new List<string>(),
                                "((frameworks/any(f: f eq 'netcoreapp')))" },
                new object[] {  new List<string>(),
                                new List<string> {"net5.0"},
                                "((tfms/any(f: f eq 'net5.0')))" },
                new object[] {  new List<string> {"net", "netstandard"},
                                new List<string> {"netcoreapp3.1"},
                                "((frameworks/any(f: f eq 'net') and frameworks/any(f: f eq 'netstandard')) and (tfms/any(f: f eq 'netcoreapp3.1')))" },
                new object[] {  new List<string> {"netframework"},
                                new List<string> {"netstandard2.1", "net40-client"},
                                "((frameworks/any(f: f eq 'netframework')) and (tfms/any(f: f eq 'netstandard2.1') and tfms/any(f: f eq 'net40-client')))" },
                new object[] {  new List<string> {"net", "netstandard"},
                                new List<string> {"netcoreapp3.1"},
                                "((computedFrameworks/any(f: f eq 'net') and computedFrameworks/any(f: f eq 'netstandard')) and (computedTfms/any(f: f eq 'netcoreapp3.1')))",
                                true },
                new object[] {  new List<string> {"netframework"},
                                new List<string> {"netstandard2.1", "net40-client"},
                                "((computedFrameworks/any(f: f eq 'netframework')) and (computedTfms/any(f: f eq 'netstandard2.1') and computedTfms/any(f: f eq 'net40-client')))",
                                true },
            };

            public static IEnumerable<object[]> ComputedFrameworkCases => new[]
            {
                new object[] {  new List<string> {"net", "netstandard"},
                                new List<string> {"netcoreapp3.1"},
                                "((computedFrameworks/any(f: f eq 'net') and computedFrameworks/any(f: f eq 'netstandard')) and (computedTfms/any(f: f eq 'netcoreapp3.1')))",
                                true },
                new object[] {  new List<string> {"net", "netstandard"},
                                new List<string> {"netcoreapp3.1"},
                                "((frameworks/any(f: f eq 'net') and frameworks/any(f: f eq 'netstandard')) and (tfms/any(f: f eq 'netcoreapp3.1')))",
                                false },
                new object[] {  new List<string> {"netframework"},
                                new List<string> {"netstandard2.1", "net462"},
                                "((computedFrameworks/any(f: f eq 'netframework')) and (computedTfms/any(f: f eq 'netstandard2.1') and computedTfms/any(f: f eq 'net462')))",
                                true },
                new object[] {  new List<string> {"netframework"},
                                new List<string> {"netstandard2.1", "net462"},
                                "((frameworks/any(f: f eq 'netframework')) and (tfms/any(f: f eq 'netstandard2.1') and tfms/any(f: f eq 'net462')))",
                                false },
            };

            public static IEnumerable<object[]> FrameworkFilterModeCases => new[]
            {
                new object[] {  new List<string> {"net", "netstandard"},
                                new List<string> {"netcoreapp3.1"},
                                null, // null
                                "((frameworks/any(f: f eq 'net') and frameworks/any(f: f eq 'netstandard')) and (tfms/any(f: f eq 'netcoreapp3.1')))" },
                new object[] {  new List<string> {"net", "netstandard"},
                                new List<string> {"netcoreapp3.1"},
                                "blah", // unexpected value
                                "((frameworks/any(f: f eq 'net') and frameworks/any(f: f eq 'netstandard')) and (tfms/any(f: f eq 'netcoreapp3.1')))" },
                new object[] {  new List<string> {"net", "netstandard"},
                                new List<string> {"netcoreapp3.1"},
                                "all",
                                "((frameworks/any(f: f eq 'net') and frameworks/any(f: f eq 'netstandard')) and (tfms/any(f: f eq 'netcoreapp3.1')))" },
                new object[] {  new List<string> {"netframework"},
                                new List<string> {"netstandard2.1", "net462"},
                                "all",
                                "((frameworks/any(f: f eq 'netframework')) and (tfms/any(f: f eq 'netstandard2.1') and tfms/any(f: f eq 'net462')))" },
                new object[] {  new List<string> {"net", "netstandard"},
                                new List<string> {"netcoreapp3.1"},
                                "aLl", // case insensitive
                                "((frameworks/any(f: f eq 'net') and frameworks/any(f: f eq 'netstandard')) and (tfms/any(f: f eq 'netcoreapp3.1')))" },
                new object[] {  new List<string> {"net", "netstandard"},
                                new List<string> {"netcoreapp3.1"},
                                "any",
                                "((frameworks/any(f: f eq 'net') or frameworks/any(f: f eq 'netstandard')) or (tfms/any(f: f eq 'netcoreapp3.1')))" },
                new object[] {  new List<string> {"netframework"},
                                new List<string> {"netstandard2.1", "net462"},
                                "any",
                                "((frameworks/any(f: f eq 'netframework')) or (tfms/any(f: f eq 'netstandard2.1') or tfms/any(f: f eq 'net462')))" },
                new object[] {  new List<string> {"net", "netstandard"},
                                new List<string> {"netcoreapp3.1"},
                                "AnY", // case insensitive
                                "((frameworks/any(f: f eq 'net') or frameworks/any(f: f eq 'netstandard')) or (tfms/any(f: f eq 'netcoreapp3.1')))" },
            };

            public BaseFacts()
            {
                _target = new SearchParametersBuilder();
            }
        }
    }
}
