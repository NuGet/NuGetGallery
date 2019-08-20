using System.Collections.Generic;
using System.Linq;
using Microsoft.Azure.Search.Models;
using Moq;
using NuGet.Indexing;
using Xunit;

namespace NuGet.Services.AzureSearch.SearchService
{
    public class IndexOperationBuilderFacts
    {
        public class Autocomplete : Facts
        {
            public override IndexOperation Build()
            {
                return Target.Autocomplete(AutocompleteRequest);
            }

            [Fact]
            public void BuildsSearchOperation()
            {
                var actual = Target.Autocomplete(AutocompleteRequest);

                Assert.Equal(IndexOperationType.Search, actual.Type);
                Assert.Same(Text, actual.SearchText);
                Assert.Same(Parameters, actual.SearchParameters);
                TextBuilder.Verify(x => x.Autocomplete(AutocompleteRequest), Times.Once);
                ParametersBuilder.Verify(x => x.Autocomplete(AutocompleteRequest, It.IsAny<bool>()), Times.Once);
            }
        }

        public class V3Search : SearchIndexFacts
        {
            public override IndexOperation Build()
            {
                return Target.V3Search(V3SearchRequest);
            }

            [Fact]
            public void CallsDependenciesForGetOperation()
            {
                ParsedQuery.Grouping[QueryField.PackageId] = new HashSet<string>(new[] { Id });

                Build();

                TextBuilder.Verify(x => x.ParseV3Search(V3SearchRequest), Times.Once);
                TextBuilder.Verify(x => x.Build(It.IsAny<ParsedQuery>()), Times.Never);
                ParametersBuilder.Verify(x => x.V3Search(It.IsAny<V3SearchRequest>(), It.IsAny<bool>()), Times.Never);
            }

            [Fact]
            public void CallsDependenciesForSearchOperation()
            {
                Build();

                TextBuilder.Verify(x => x.ParseV3Search(V3SearchRequest), Times.Once);
                TextBuilder.Verify(x => x.Build(ParsedQuery), Times.Once);
                ParametersBuilder.Verify(x => x.V3Search(V3SearchRequest, It.IsAny<bool>()), Times.Once);
            }
        }

        public class V2SearchWithSearchIndex : SearchIndexFacts
        {
            public override IndexOperation Build()
            {
                return Target.V2SearchWithSearchIndex(V2SearchRequest);
            }

            [Fact]
            public void CallsDependenciesForGetOperation()
            {
                ParsedQuery.Grouping[QueryField.PackageId] = new HashSet<string>(new[] { Id });

                Build();

                TextBuilder.Verify(x => x.ParseV2Search(V2SearchRequest), Times.Once);
                TextBuilder.Verify(x => x.Build(It.IsAny<ParsedQuery>()), Times.Never);
                ParametersBuilder.Verify(x => x.V2Search(It.IsAny<V2SearchRequest>(), It.IsAny<bool>()), Times.Never);
            }

            [Fact]
            public void CallsDependenciesForSearchOperation()
            {
                Build();

                TextBuilder.Verify(x => x.ParseV2Search(V2SearchRequest), Times.Once);
                TextBuilder.Verify(x => x.Build(ParsedQuery), Times.Once);
                ParametersBuilder.Verify(x => x.V2Search(V2SearchRequest, It.IsAny<bool>()), Times.Once);
            }
        }

        public class V2SearchWithHijackIndex : Facts
        {
            public override IndexOperation Build()
            {
                return Target.V2SearchWithHijackIndex(V2SearchRequest);
            }

            [Theory]
            [MemberData(nameof(ValidIdData))]
            public void BuildsGetOperationForSingleValidPackageIdAndSingleValidVersion(string id)
            {
                ParsedQuery.Grouping[QueryField.PackageId] = new HashSet<string>(new[] { id });
                ParsedQuery.Grouping[QueryField.Version] = new HashSet<string>(new[] { Version });

                var actual = Build();

                Assert.Equal(IndexOperationType.Get, actual.Type);
                Assert.Equal(
                    DocumentUtilities.GetHijackDocumentKey(id, Version),
                    actual.DocumentKey);
            }

            [Theory]
            [MemberData(nameof(InvalidIdData))]
            public void DoesNotBuildGetOperationForSingleInvalidPackageIdAndSingleValidVersion(string id)
            {
                ParsedQuery.Grouping[QueryField.PackageId] = new HashSet<string>(new[] { id });
                ParsedQuery.Grouping[QueryField.Version] = new HashSet<string>(new[] { Version });

                var actual = Build();

                Assert.Equal(IndexOperationType.Search, actual.Type);
            }

            [Theory]
            [InlineData("\"1.0.0\"")]
            [InlineData("1.0.0.0.0")]
            [InlineData("1.0.0.a")]
            [InlineData("1.0.0.-alpha")]
            [InlineData("1.0.0-beta.01")]
            [InlineData("alpha")]
            [InlineData("")]
            public void DoesNotBuildGetOperationForSingleValidPackageIdAndSingleInvalidVersion(string version)
            {
                ParsedQuery.Grouping[QueryField.PackageId] = new HashSet<string>(new[] { Id });
                ParsedQuery.Grouping[QueryField.Version] = new HashSet<string>(new[] { version });

                var actual = Build();

                Assert.Equal(IndexOperationType.Search, actual.Type);
            }

            [Theory]
            [InlineData("1.0.0", "1.0.0")]
            [InlineData("1.0.0-BETA", "1.0.0-BETA")]
            [InlineData("1.0.0-beta01", "1.0.0-beta01")]
            [InlineData("1.0.0-beta.2", "1.0.0-beta.2")]
            [InlineData("1.0.0.0", "1.0.0")]
            [InlineData("1.0.0-ALPHA+git", "1.0.0-ALPHA")]
            [InlineData("1.0.0-alpha+git", "1.0.0-alpha")]
            [InlineData("1.0.00-alpha", "1.0.0-alpha")]
            [InlineData("1.0.01-alpha", "1.0.1-alpha")]
            [InlineData("   1.0.0   ", "1.0.0")]
            public void NormalizesVersion(string version, string normalized)
            {
                ParsedQuery.Grouping[QueryField.PackageId] = new HashSet<string>(new[] { Id });
                ParsedQuery.Grouping[QueryField.Version] = new HashSet<string>(new[] { version });

                var actual = Build();

                Assert.Equal(IndexOperationType.Get, actual.Type);
                Assert.Equal(
                    DocumentUtilities.GetHijackDocumentKey(Id, normalized),
                    actual.DocumentKey);
            }

            [Fact]
            public void IgnoresFiltersWithSpecificPackageIdAndVersion()
            {
                V2SearchRequest.IncludePrerelease = false;
                V2SearchRequest.IncludeSemVer2 = false;
                var prereleaseSemVer2 = "1.0.0-beta.1";
                ParsedQuery.Grouping[QueryField.PackageId] = new HashSet<string>(new[] { Id });
                ParsedQuery.Grouping[QueryField.Version] = new HashSet<string>(new[] { prereleaseSemVer2 });

                var actual = Build();

                Assert.Equal(IndexOperationType.Get, actual.Type);
                Assert.Equal(
                    DocumentUtilities.GetHijackDocumentKey(Id, prereleaseSemVer2),
                    actual.DocumentKey);
            }

            [Fact]
            public void DoesNotBuildGetOperationForNonPackageIdAndVersion()
            {
                ParsedQuery.Grouping[QueryField.Id] = new HashSet<string>(new[] { Id });
                ParsedQuery.Grouping[QueryField.Version] = new HashSet<string>(new[] { Version });

                var actual = Build();

                Assert.Equal(IndexOperationType.Search, actual.Type);
            }

            [Fact]
            public void DoesNotBuildGetOperationForMultiplePackageIds()
            {
                ParsedQuery.Grouping[QueryField.PackageId] = new HashSet<string>(new[] { Id, "A" });
                ParsedQuery.Grouping[QueryField.Version] = new HashSet<string>(new[] { Version });

                var actual = Build();

                Assert.Equal(IndexOperationType.Search, actual.Type);
            }

            [Fact]
            public void DoesNotBuildGetOperationForMultipleVersions()
            {
                ParsedQuery.Grouping[QueryField.PackageId] = new HashSet<string>(new[] { Id });
                ParsedQuery.Grouping[QueryField.Version] = new HashSet<string>(new[] { Version, "9.9.9" });

                var actual = Build();

                Assert.Equal(IndexOperationType.Search, actual.Type);
            }

            [Fact]
            public void DoesNotBuildGetOperationForPackageIdVersionAndExtra()
            {
                ParsedQuery.Grouping[QueryField.PackageId] = new HashSet<string>(new[] { Id });
                ParsedQuery.Grouping[QueryField.Version] = new HashSet<string>(new[] { Version });
                ParsedQuery.Grouping[QueryField.Description] = new HashSet<string>(new[] { "hi" });

                var actual = Build();

                Assert.Equal(IndexOperationType.Search, actual.Type);
            }

            [Fact]
            public void DoesNotBuildGetOperationForEmptyPackageId()
            {
                ParsedQuery.Grouping[QueryField.PackageId] = new HashSet<string>();
                ParsedQuery.Grouping[QueryField.Version] = new HashSet<string>(new[] { Version });

                var actual = Build();

                Assert.Equal(IndexOperationType.Search, actual.Type);
            }

            [Fact]
            public void DoesNotBuildGetOperationForEmptyVersion()
            {
                ParsedQuery.Grouping[QueryField.PackageId] = new HashSet<string>(new[] { Id });
                ParsedQuery.Grouping[QueryField.Version] = new HashSet<string>();

                var actual = Build();

                Assert.Equal(IndexOperationType.Search, actual.Type);
            }

            [Fact]
            public void DoesNotBuildGetOperationForSkippingFirstItem()
            {
                V2SearchRequest.Skip = 1;
                ParsedQuery.Grouping[QueryField.PackageId] = new HashSet<string>(new[] { Id });
                ParsedQuery.Grouping[QueryField.Version] = new HashSet<string>(new[] { Version });

                var actual = Build();

                Assert.Equal(IndexOperationType.Search, actual.Type);
            }

            [Fact]
            public void DoesNotBuildGetOperationForNoTake()
            {
                V2SearchRequest.Take = 0;
                ParsedQuery.Grouping[QueryField.PackageId] = new HashSet<string>(new[] { Id });
                ParsedQuery.Grouping[QueryField.Version] = new HashSet<string>(new[] { Version });

                var actual = Build();

                Assert.Equal(IndexOperationType.Search, actual.Type);
            }

            [Fact]
            public void CallsDependenciesForGetOperation()
            {
                ParsedQuery.Grouping[QueryField.PackageId] = new HashSet<string>(new[] { Id });
                ParsedQuery.Grouping[QueryField.Version] = new HashSet<string>(new[] { Version });

                Build();

                TextBuilder.Verify(x => x.ParseV2Search(V2SearchRequest), Times.Once);
                TextBuilder.Verify(x => x.Build(It.IsAny<ParsedQuery>()), Times.Never);
                ParametersBuilder.Verify(x => x.V2Search(It.IsAny<V2SearchRequest>(), It.IsAny<bool>()), Times.Never);
            }

            [Fact]
            public void CallsDependenciesForSearchOperation()
            {
                Build();

                TextBuilder.Verify(x => x.ParseV2Search(V2SearchRequest), Times.Once);
                TextBuilder.Verify(x => x.Build(ParsedQuery), Times.Once);
                ParametersBuilder.Verify(x => x.V2Search(V2SearchRequest, It.IsAny<bool>()), Times.Once);
            }
        }

        public abstract class SearchIndexFacts : Facts
        {
            [Theory]
            [MemberData(nameof(ValidIdData))]
            public void BuildsGetOperationForSingleValidPackageId(string id)
            {
                ParsedQuery.Grouping[QueryField.PackageId] = new HashSet<string>(new[] { id });

                var actual = Build();

                Assert.Equal(IndexOperationType.Get, actual.Type);
                Assert.Equal(
                    DocumentUtilities.GetSearchDocumentKey(id, SearchFilters.Default),
                    actual.DocumentKey);
            }

            [Theory]
            [MemberData(nameof(InvalidIdData))]
            public void DoesNotBuildGetOperationForInvalidPackageId(string id)
            {
                ParsedQuery.Grouping[QueryField.PackageId] = new HashSet<string>(new[] { id });

                var actual = Build();

                Assert.Equal(IndexOperationType.Search, actual.Type);
            }

            [Fact]
            public void DoesNotBuildGetOperationForEmptyPackageIdQuery()
            {
                ParsedQuery.Grouping[QueryField.PackageId] = new HashSet<string>();

                var actual = Build();

                Assert.Equal(IndexOperationType.Search, actual.Type);
            }

            [Fact]
            public void DoesNotBuildGetOperationForIdQuery()
            {
                ParsedQuery.Grouping[QueryField.Id] = new HashSet<string>(new[] { Id });

                var actual = Build();

                Assert.Equal(IndexOperationType.Search, actual.Type);
            }

            [Fact]
            public void DoesNotBuildGetOperationForSkippingFirstItem()
            {
                V2SearchRequest.Skip = 1;
                V3SearchRequest.Skip = 1;
                ParsedQuery.Grouping[QueryField.PackageId] = new HashSet<string>(new[] { Id });

                var actual = Build();

                Assert.Equal(IndexOperationType.Search, actual.Type);
            }

            [Fact]
            public void DoesNotBuildGetOperationForNoTake()
            {
                V2SearchRequest.Take = 0;
                V3SearchRequest.Take = 0;
                ParsedQuery.Grouping[QueryField.PackageId] = new HashSet<string>(new[] { Id });

                var actual = Build();

                Assert.Equal(IndexOperationType.Search, actual.Type);
            }

            [Fact]
            public void DoesNotBuildGetOperationForPackageIdVersion()
            {
                ParsedQuery.Grouping[QueryField.PackageId] = new HashSet<string>(new[] { Id });
                ParsedQuery.Grouping[QueryField.Version] = new HashSet<string>(new[] { Version });

                var actual = Build();

                Assert.Equal(IndexOperationType.Search, actual.Type);
            }

            [Fact]
            public void DoesNotBuildGetOperationForMultipleFields()
            {
                ParsedQuery.Grouping[QueryField.PackageId] = new HashSet<string>(new[] { Id });
                ParsedQuery.Grouping[QueryField.Description] = new HashSet<string>(new[] { "hi" });

                var actual = Build();

                Assert.Equal(IndexOperationType.Search, actual.Type);
            }

            [Fact]
            public void DoesNotBuildGetOperationForMultiplePackageIds()
            {
                ParsedQuery.Grouping[QueryField.PackageId] = new HashSet<string>(new[] { Id, "A" });

                var actual = Build();

                Assert.Equal(IndexOperationType.Search, actual.Type);
            }

            [Fact]
            public void BuildsSearchOperationForNonPackageIdQueries()
            {
                var actual = Build();

                Assert.Equal(IndexOperationType.Search, actual.Type);
            }
        }

        public abstract class Facts
        {
            public const string Id = "NuGet.Versioning";
            public const string Version = "5.1.0";

            public const int MaxIdLength = 100;

            public static IReadOnlyList<string> ValidIds => new[]
            {
                Id,
                new string('a', MaxIdLength),
                "A",
                "Foo__Bar",
            };

            public static IReadOnlyList<string> InvalidIds => new[]
            {
                string.Empty,
                "   ",
                "\nNuGet.Versioning",
                "A,B",
                "foo--bar",
                "foo..bar",
                "   NuGet.Versioning   ",
                "   NuGet.Versioning",
                "NuGet.Versioning   ",
                "\"NuGet.Versioning\"",
                new string('a', MaxIdLength + 1),
            };

            public static IEnumerable<object[]> ValidIdData => ValidIds.Select(x => new object[] { x });
            public static IEnumerable<object[]> InvalidIdData => InvalidIds.Select(x => new object[] { x });

            public static IEnumerable<object[]> TooLargeSkip => new[]
            {
                new object[] { 99999, IndexOperationType.Search },
                new object[] { 100000, IndexOperationType.Search },
                new object[] { 100001, IndexOperationType.Empty },
                new object[] { 100002, IndexOperationType.Empty },
            };

            public Facts()
            {
                TextBuilder = new Mock<ISearchTextBuilder>();
                ParametersBuilder = new Mock<ISearchParametersBuilder>();

                AutocompleteRequest = new AutocompleteRequest { Skip = 0, Take = 20 };
                V2SearchRequest = new V2SearchRequest { Skip = 0, Take = 20 };
                V3SearchRequest = new V3SearchRequest { Skip = 0, Take = 20 };
                Text = "";
                Parameters = new SearchParameters();
                ParsedQuery = new ParsedQuery(new Dictionary<QueryField, HashSet<string>>());

                TextBuilder
                    .Setup(x => x.Autocomplete(It.IsAny<AutocompleteRequest>()))
                    .Returns(() => Text);
                TextBuilder
                    .Setup(x => x.ParseV2Search(It.IsAny<V2SearchRequest>()))
                    .Returns(() => ParsedQuery);
                TextBuilder
                    .Setup(x => x.ParseV3Search(It.IsAny<V3SearchRequest>()))
                    .Returns(() => ParsedQuery);
                TextBuilder
                    .Setup(x => x.Build(It.IsAny<ParsedQuery>()))
                    .Returns(() => Text);
                ParametersBuilder
                    .Setup(x => x.Autocomplete(It.IsAny<AutocompleteRequest>(), It.IsAny<bool>()))
                    .Returns(() => Parameters);
                ParametersBuilder
                    .Setup(x => x.V2Search(It.IsAny<V2SearchRequest>(), It.IsAny<bool>()))
                    .Returns(() => Parameters);
                ParametersBuilder
                    .Setup(x => x.V3Search(It.IsAny<V3SearchRequest>(), It.IsAny<bool>()))
                    .Returns(() => Parameters);

                Target = new IndexOperationBuilder(
                    TextBuilder.Object,
                    ParametersBuilder.Object);
            }

            public Mock<ISearchTextBuilder> TextBuilder { get; }
            public Mock<ISearchParametersBuilder> ParametersBuilder { get; }
            public AutocompleteRequest AutocompleteRequest { get; }
            public V2SearchRequest V2SearchRequest { get; }
            public V3SearchRequest V3SearchRequest { get; }
            public string Text { get; }
            public SearchParameters Parameters { get; }
            public ParsedQuery ParsedQuery { get; }
            public IndexOperationBuilder Target { get; }

            public abstract IndexOperation Build();

            [Theory]
            [MemberData(nameof(TooLargeSkip))]
            public void ReturnsEmptyOperationForTooLargeSkip(int skip, IndexOperationType expected)
            {
                AutocompleteRequest.Skip = skip;
                V2SearchRequest.Skip = skip;
                V3SearchRequest.Skip = skip;

                var actual = Build();

                Assert.Equal(expected, actual.Type);
            }
        }
    }
}
