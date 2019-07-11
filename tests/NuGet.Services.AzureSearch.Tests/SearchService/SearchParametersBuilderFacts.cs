// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Azure.Search.Models;
using Xunit;

namespace NuGet.Services.AzureSearch.SearchService
{
    public class SearchParametersBuilderFacts
    {
        public class LastCommitTimestamp : BaseFacts
        {
            [Fact]
            public void Defaults()
            {
                var output = _target.LastCommitTimestamp();

                Assert.Equal(QueryType.Full, output.QueryType);
                Assert.False(output.IncludeTotalResultCount);
                Assert.Equal(new[] { "lastCommitTimestamp desc" }, output.OrderBy.ToArray());
                Assert.Equal(0, output.Skip);
                Assert.Equal(1, output.Top);
                Assert.Null(output.Filter);
            }
        }

        public class V2Search : BaseFacts
        {
            [Fact]
            public void Defaults()
            {
                var request = new V2SearchRequest();

                var output = _target.V2Search(request);

                Assert.Equal(QueryType.Full, output.QueryType);
                Assert.True(output.IncludeTotalResultCount);
                Assert.Null(output.OrderBy);
                Assert.Equal(0, output.Skip);
                Assert.Equal(0, output.Top);
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

                var output = _target.V2Search(request);

                Assert.Equal(QueryType.Full, output.QueryType);
                Assert.True(output.IncludeTotalResultCount);
                Assert.Null(output.OrderBy);
                Assert.Equal(0, output.Skip);
                Assert.Equal(0, output.Top);
            }

            [Fact]
            public void Paging()
            {
                var request = new V2SearchRequest
                {
                    Skip = 10,
                    Take = 30,
                };

                var output = _target.V2Search(request);

                Assert.Equal(10, output.Skip);
                Assert.Equal(30, output.Top);
            }

            [Fact]
            public void NegativeSkip()
            {
                var request = new V2SearchRequest
                {
                    Skip = -10,
                };

                var output = _target.V2Search(request);

                Assert.Equal(0, output.Skip);
            }

            [Fact]
            public void NegativeTake()
            {
                var request = new V2SearchRequest
                {
                    Take = -20,
                };

                var output = _target.V2Search(request);

                Assert.Equal(20, output.Top);
            }

            [Fact]
            public void TooLargeTake()
            {
                var request = new V2SearchRequest
                {
                    Take = 1001,
                };

                var output = _target.V2Search(request);

                Assert.Equal(20, output.Top);
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

                var output = _target.V2Search(request);

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

                var output = _target.V2Search(request);

                if (expectedOrderBy == null)
                {
                    Assert.Null(output.OrderBy);
                }
                else
                {
                    Assert.Equal(expectedOrderBy, Assert.Single(output.OrderBy));
                }
            }

            [Theory]
            [MemberData(nameof(AllSearchFilters))]
            public void SearchFilters(bool includePrerelease, bool includeSemVer2, string filter)
            {
                var request = new V2SearchRequest
                {
                    IncludePrerelease = includePrerelease,
                    IncludeSemVer2 = includeSemVer2,
                };

                var output = _target.V2Search(request);

                Assert.Equal(filter, output.Filter);
            }
        }

        public class V3Search : BaseFacts
        {
            [Fact]
            public void Defaults()
            {
                var request = new V3SearchRequest();

                var output = _target.V3Search(request);

                Assert.Equal(QueryType.Full, output.QueryType);
                Assert.True(output.IncludeTotalResultCount);
                Assert.Null(output.OrderBy);
                Assert.Equal(0, output.Skip);
                Assert.Equal(0, output.Top);
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

                var output = _target.V3Search(request);

                Assert.Equal(10, output.Skip);
                Assert.Equal(30, output.Top);
            }

            [Fact]
            public void NegativeSkip()
            {
                var request = new V3SearchRequest
                {
                    Skip = -10,
                };

                var output = _target.V3Search(request);

                Assert.Equal(0, output.Skip);
            }

            [Fact]
            public void NegativeTake()
            {
                var request = new V3SearchRequest
                {
                    Take = -20,
                };

                var output = _target.V3Search(request);

                Assert.Equal(20, output.Top);
            }

            [Fact]
            public void TooLargeTake()
            {
                var request = new V3SearchRequest
                {
                    Take = 1001,
                };

                var output = _target.V3Search(request);

                Assert.Equal(20, output.Top);
            }

            [Theory]
            [MemberData(nameof(AllSearchFilters))]
            public void SearchFilters(bool includePrerelease, bool includeSemVer2, string filter)
            {
                var request = new V3SearchRequest
                {
                    IncludePrerelease = includePrerelease,
                    IncludeSemVer2 = includeSemVer2,
                };

                var output = _target.V3Search(request);

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

                var output = _target.Autocomplete(request);

                Assert.Equal(QueryType.Full, output.QueryType);
                Assert.True(output.IncludeTotalResultCount);
                Assert.Null(output.OrderBy);
                Assert.Equal(0, output.Skip);
                Assert.Equal(0, output.Top);
                Assert.Equal("searchFilters eq 'Default'", output.Filter);
                Assert.Single(output.Select);
                Assert.Equal(IndexFields.PackageId, output.Select[0]);
            }

            [Fact]
            public void PackageVersionsDefaults()
            {
                var request = new AutocompleteRequest();
                request.Type = AutocompleteRequestType.PackageVersions;

                var output = _target.Autocomplete(request);

                Assert.Equal(QueryType.Full, output.QueryType);
                Assert.True(output.IncludeTotalResultCount);
                Assert.Null(output.OrderBy);
                Assert.Equal(0, output.Skip);
                Assert.Equal(1, output.Top);
                Assert.Equal("searchFilters eq 'Default'", output.Filter);
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

                var output = _target.Autocomplete(request);

                Assert.Equal(10, output.Skip);
                Assert.Equal(30, output.Top);
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

                var output = _target.Autocomplete(request);

                Assert.Equal(0, output.Skip);
                Assert.Equal(1, output.Top);
            }

            [Fact]
            public void NegativeSkip()
            {
                var request = new AutocompleteRequest
                {
                    Skip = -10,
                    Type = AutocompleteRequestType.PackageIds,
                };

                var output = _target.Autocomplete(request);

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

                var output = _target.Autocomplete(request);

                Assert.Equal(20, output.Top);
            }

            [Fact]
            public void TooLargeTake()
            {
                var request = new AutocompleteRequest
                {
                    Type = AutocompleteRequestType.PackageIds,
                    Take = 1001,
                };

                var output = _target.Autocomplete(request);

                Assert.Equal(20, output.Top);
            }

            [Theory]
            [MemberData(nameof(AllSearchFilters))]
            public void SearchFilters(bool includePrerelease, bool includeSemVer2, string filter)
            {
                var request = new AutocompleteRequest
                {
                    IncludePrerelease = includePrerelease,
                    IncludeSemVer2 = includeSemVer2,
                };

                var output = _target.Autocomplete(request);

                Assert.Equal(filter, output.Filter);
            }
        }

        public abstract class BaseFacts
        {
            protected readonly SearchParametersBuilder _target;

            public static Dictionary<V2SortBy, string> V2SortByToOrderBy => new Dictionary<V2SortBy, string>
            {
                { V2SortBy.LastEditedDescending, "lastEdited desc" },
                { V2SortBy.Popularity, null },
                { V2SortBy.PublishedDescending, "published desc" },
                { V2SortBy.SortableTitleAsc, "sortableTitle asc" },
                { V2SortBy.SortableTitleDesc, "sortableTitle desc" },
            };

            public static IEnumerable<object[]> AllSearchFilters => new[]
            {
                new object[] { false, false, "searchFilters eq 'Default'" },
                new object[] { true, false, "searchFilters eq 'IncludePrerelease'" },
                new object[] { false, true, "searchFilters eq 'IncludeSemVer2'" },
                new object[] { true, true, "searchFilters eq 'IncludePrereleaseAndSemVer2'" },
            };

            public static IEnumerable<object[]> AllV2SortBy => Enum
                .GetValues(typeof(V2SortBy))
                .Cast<V2SortBy>()
                .Select(x => new object[] { x });

            public BaseFacts()
            {
                _target = new SearchParametersBuilder();
            }
        }
    }
}
