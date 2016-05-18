// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using Lucene.Net.Index;
using Lucene.Net.Search;
using NuGet.Indexing;
using Xunit;

namespace NuGet.IndexingTests
{
    public class NuGetQueryTests
    {
        [Theory]
        [MemberData(nameof(MakesQueriesWithProperPhrasingData))]
        public void MakesQueriesWithProperPhrasing(string input, Query expected)
        {
            // arrange, act
            var actual = NuGetQuery.MakeQuery(input);

            // assert
            Assert.Equal(expected, actual);
        }

        [Theory]
        [MemberData(nameof(MakesQueriesSupportingSupportedFieldsData))]
        public void MakesQueriesSupportingSupportedFields(string input, Query expected)
        {
            // act
            var actual = NuGetQuery.MakeQuery(input);

            // assert
            Assert.Equal(expected, actual);
        }

        [Theory]
        [MemberData(nameof(MakesQueriesSupportingFieldAliasesData))]
        public void MakesQueriesSupportingFieldAliases(string inputField, string expectedField)
        {
            // arrange
            var queryText = $"{inputField}:dot";

            // act
            var actual = NuGetQuery.MakeQuery(queryText);

            // assert
            Assert.Contains($"{expectedField}:dot", actual.ToString());
        }

        [Theory]
        [MemberData(nameof(MakesFilteredQueriesSupportingFieldAliasesData))]
        public void MakesFilteredQueriesSupportingFieldAliases(string inputField, string expectedField)
        {
            // arrange
            var owners = CreateOwnersResult(new Dictionary<string, HashSet<string>>
                {
                    {  "dot", new HashSet<string> { "dot" } }
                });
            var queryText = $"{inputField}:dot";

            // act
            var actual = NuGetQuery.MakeQuery(queryText, owners);

            // assert
            Assert.Contains("filtered(*:*)->NuGet.Indexing.OwnersFilter", actual.ToString());
        }

        [Fact]
        public void AutomaticallyClosesDanglingQuotes()
        {
            // arrange
            var queryText = "title:\"dot NET version:1.2.3";
            var phraseQuery = new PhraseQuery();
            phraseQuery.Add(new Term("Title", "dot"));
            phraseQuery.Add(new Term("Title", "net"));
            phraseQuery.Add(new Term("Title", "version"));
            phraseQuery.Add(new Term("Title", "1"));
            phraseQuery.Add(new Term("Title", "2"));
            phraseQuery.Add(new Term("Title", "3"));

            var expected = new BooleanQuery
            {
                new BooleanClause(new BooleanQuery { new BooleanClause(new BooleanQuery { new BooleanClause(phraseQuery, Occur.SHOULD) }, Occur.SHOULD) }, Occur.MUST)
            };

            // act
            var actual = NuGetQuery.MakeQuery(queryText);

            // assert
            Assert.Equal(expected, actual);
        }

        [Fact]
        public void TreatsNoFieldLabelAsQueryingAllFields()
        {
            // arrange
            var queryText = "dot";
            var expected = new BooleanQuery
            {
                new BooleanClause(new BooleanQuery { Clauses = { new BooleanClause(new TermQuery(new Term("Id", "dot")), Occur.SHOULD) }, Boost = 8 }, Occur.SHOULD),
                new BooleanClause(new BooleanQuery { new BooleanClause(new TermQuery(new Term("ShingledId", "dot")), Occur.SHOULD) }, Occur.SHOULD),
                new BooleanClause(new BooleanQuery { new BooleanClause(new TermQuery(new Term("TokenizedId", "dot")), Occur.SHOULD) }, Occur.SHOULD),
                new BooleanClause(new BooleanQuery { new BooleanClause(new TermQuery(new Term("Version", "dot")), Occur.SHOULD) }, Occur.SHOULD),
                new BooleanClause(new BooleanQuery { new BooleanClause(new TermQuery(new Term("Title", "dot")), Occur.SHOULD) }, Occur.SHOULD),
                new BooleanClause(new BooleanQuery { new BooleanClause(new TermQuery(new Term("Description", "dot")), Occur.SHOULD) }, Occur.SHOULD),
                new BooleanClause(new BooleanQuery { new BooleanClause(new TermQuery(new Term("Summary", "dot")), Occur.SHOULD) }, Occur.SHOULD),
                new BooleanClause(new BooleanQuery { Clauses = { new BooleanClause(new TermQuery(new Term("Tags", "dot")), Occur.SHOULD) }, Boost = 2 }, Occur.SHOULD),
                new BooleanClause(new BooleanQuery { new BooleanClause(new TermQuery(new Term("Authors", "dot")), Occur.SHOULD) }, Occur.SHOULD)
            };

            // act
            var actual = NuGetQuery.MakeQuery(queryText);

            // assert
            Assert.Equal(expected, actual);
        }

        [Fact]
        public void CanMixTermsWithAndWithoutFieldLabels()
        {
            // arrange
            var owners = CreateOwnersResult(new Dictionary<string, HashSet<string>>
                {
                    {  "dot", new HashSet<string> { "microsoft" } }
                });

            var queryText = "dot owner:Microsoft";
            var expected = new FilteredQuery(
                new BooleanQuery
                {
                    new BooleanClause(new BooleanQuery { Clauses = { new BooleanClause(new TermQuery(new Term("Id", "dot")), Occur.SHOULD) }, Boost = 8 }, Occur.SHOULD),
                    new BooleanClause(new BooleanQuery { new BooleanClause(new TermQuery(new Term("ShingledId", "dot")), Occur.SHOULD) }, Occur.SHOULD),
                    new BooleanClause(new BooleanQuery { new BooleanClause(new TermQuery(new Term("TokenizedId", "dot")), Occur.SHOULD) }, Occur.SHOULD),
                    new BooleanClause(new BooleanQuery { new BooleanClause(new TermQuery(new Term("Version", "dot")), Occur.SHOULD) }, Occur.SHOULD),
                    new BooleanClause(new BooleanQuery { new BooleanClause(new TermQuery(new Term("Title", "dot")), Occur.SHOULD) }, Occur.SHOULD),
                    new BooleanClause(new BooleanQuery { new BooleanClause(new TermQuery(new Term("Description", "dot")), Occur.SHOULD) }, Occur.SHOULD),
                    new BooleanClause(new BooleanQuery { new BooleanClause(new TermQuery(new Term("Summary", "dot")), Occur.SHOULD) }, Occur.SHOULD),
                    new BooleanClause(new BooleanQuery { Clauses = { new BooleanClause(new TermQuery(new Term("Tags", "dot")), Occur.SHOULD) }, Boost = 2 }, Occur.SHOULD),
                    new BooleanClause(new BooleanQuery { new BooleanClause(new TermQuery(new Term("Authors", "dot")), Occur.SHOULD) }, Occur.SHOULD)
                },
                new OwnersFilter(owners, "Microsoft"));

            // act
            var actual = NuGetQuery.MakeQuery(queryText, owners);

            // assert
            Assert.Equal(expected.ToString(), actual.ToString());
        }

        private OwnersResult CreateOwnersResult(Dictionary<string, HashSet<string>> originalPackagesWithOwners)
        {
            var knownOwners = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var packagesWithOwners = new Dictionary<string, HashSet<string>>(StringComparer.OrdinalIgnoreCase);
            var mappings = new Dictionary<string, IDictionary<string, DynamicDocIdSet>>(StringComparer.OrdinalIgnoreCase)
            {
                { "", new Dictionary<string, DynamicDocIdSet>(StringComparer.OrdinalIgnoreCase) }
            };

            foreach (var originalPackageWithOwners in originalPackagesWithOwners)
            {
                var originalOwners = new HashSet<string>();

                foreach (var owner in originalPackageWithOwners.Value)
                {
                    knownOwners.Add(owner);
                    originalOwners.Add(owner);
                }

                packagesWithOwners.Add(originalPackageWithOwners.Key, originalOwners);
            }

            return new OwnersResult(knownOwners, packagesWithOwners, mappings);
        }

        [Fact]
        public void EmptyQueryMatchesAllDocuments()
        {
            // arrange
            var queryText = string.Empty;
            var expected = new MatchAllDocsQuery();

            // act
            var actual = NuGetQuery.MakeQuery(queryText);

            // assert
            Assert.Equal(expected, actual);
        }

        [Fact]
        public void TreatsUnrecognizedFieldAsAnyField()
        {
            // arrange
            var queryText = "invalid:dot";
            var expected = new BooleanQuery
            {
                new BooleanClause(new BooleanQuery { Clauses = { new BooleanClause(new TermQuery(new Term("Id", "dot")), Occur.SHOULD) }, Boost = 8 }, Occur.SHOULD),
                new BooleanClause(new BooleanQuery { new BooleanClause(new TermQuery(new Term("ShingledId", "dot")), Occur.SHOULD) }, Occur.SHOULD),
                new BooleanClause(new BooleanQuery { new BooleanClause(new TermQuery(new Term("TokenizedId", "dot")), Occur.SHOULD) }, Occur.SHOULD),
                new BooleanClause(new BooleanQuery { new BooleanClause(new TermQuery(new Term("Version", "dot")), Occur.SHOULD) }, Occur.SHOULD),
                new BooleanClause(new BooleanQuery { new BooleanClause(new TermQuery(new Term("Title", "dot")), Occur.SHOULD) }, Occur.SHOULD),
                new BooleanClause(new BooleanQuery { new BooleanClause(new TermQuery(new Term("Description", "dot")), Occur.SHOULD) }, Occur.SHOULD),
                new BooleanClause(new BooleanQuery { new BooleanClause(new TermQuery(new Term("Summary", "dot")), Occur.SHOULD) }, Occur.SHOULD),
                new BooleanClause(new BooleanQuery { Clauses = { new BooleanClause(new TermQuery(new Term("Tags", "dot")), Occur.SHOULD) }, Boost = 2 }, Occur.SHOULD),
                new BooleanClause(new BooleanQuery { new BooleanClause(new TermQuery(new Term("Authors", "dot")), Occur.SHOULD) }, Occur.SHOULD)
            };

            // act
            var actual = NuGetQuery.MakeQuery(queryText);

            // assert
            Assert.Equal(expected, actual);
        }

        public static IEnumerable<object[]> MakesQueriesWithProperPhrasingData
        {
            get
            {
                // multiphrase query
                {
                    var multiPhraseQuery = new MultiPhraseQuery();
                    multiPhraseQuery.Add(new[] { new Term("Title", "dotnetzip"), new Term("Title", "dot"), new Term("Title", "dotnet") });
                    multiPhraseQuery.Add(new[] { new Term("Title", "net"), new Term("Title", "netzip") });
                    multiPhraseQuery.Add(new[] { new Term("Title", "zip") });

                    yield return new object[]
                    {
                        "TITLE:\"DotNetZip\"",
                        new BooleanQuery
                        {
                            new BooleanClause(new BooleanQuery
                            {
                                new BooleanClause(new BooleanQuery
                                {
                                    new BooleanClause(multiPhraseQuery, Occur.SHOULD)
                                }, Occur.SHOULD)
                            }, Occur.MUST)
                        }
                    };
                }

                // phrase query
                {
                    var phraseQuery = new PhraseQuery();
                    phraseQuery.Add(new Term("Title", "dot"));
                    phraseQuery.Add(new Term("Title", "net"));

                    yield return new object[]
                    {
                        "TITLE:\"dot net\"",
                        new BooleanQuery
                        {
                            new BooleanClause(new BooleanQuery
                            {
                                new BooleanClause(new BooleanQuery
                                {
                                    new BooleanClause(phraseQuery, Occur.SHOULD)
                                }, Occur.SHOULD)
                            }, Occur.MUST)
                        }
                    };
                }

                // term query
                yield return new object[]
                {
                    "TITLE:\"dot\"",
                    new BooleanQuery
                    {
                        new BooleanClause(new BooleanQuery
                        {
                            new BooleanClause(new BooleanQuery
                            {
                                new BooleanClause(new TermQuery(new Term("Title", "dot")), Occur.SHOULD)
                            }, Occur.SHOULD)
                        }, Occur.MUST)
                    }
                };
            }
        }

        public static IEnumerable<object[]> MakesQueriesSupportingSupportedFieldsData
        {
            get
            {
                // id
                yield return new object[]
                {
                    "id:Aa id:Bb",
                    new BooleanQuery
                    {
                        new BooleanClause(new BooleanQuery
                        {
                            new BooleanClause(new BooleanQuery
                            {
                                Clauses =
                                {
                                    new BooleanClause(new TermQuery(new Term("Id", "aa")), Occur.SHOULD),
                                    new BooleanClause(new TermQuery(new Term("Id", "bb")), Occur.SHOULD)
                                },
                                Boost = 8
                            }, Occur.SHOULD),
                            new BooleanClause(new BooleanQuery
                            {
                                new BooleanClause(new TermQuery(new Term("ShingledId", "aa")), Occur.SHOULD),
                                new BooleanClause(new TermQuery(new Term("ShingledId", "bb")), Occur.SHOULD)
                            }, Occur.SHOULD),
                            new BooleanClause(new BooleanQuery
                            {
                                new BooleanClause(new TermQuery(new Term("TokenizedId", "aa")), Occur.SHOULD),
                                new BooleanClause(new TermQuery(new Term("TokenizedId", "bb")), Occur.SHOULD)
                            }, Occur.SHOULD),
                            new BooleanClause(new BooleanQuery
                            {
                                Clauses =
                                {
                                    new BooleanClause(new TermQuery(new Term("TokenizedId", "aa")), Occur.MUST),
                                    new BooleanClause(new TermQuery(new Term("TokenizedId", "bb")), Occur.MUST)
                                },
                                Boost = 4
                            }, Occur.SHOULD)
                        }, Occur.MUST)
                    }
                };

                // version
                yield return GetSimpleFieldQuery("Version");
                
                // title
                yield return new object[]
                {
                    "title:dot title:net",
                    new BooleanQuery
                    {
                        new BooleanClause(new BooleanQuery
                        {
                            new BooleanClause(new BooleanQuery
                            {
                                new BooleanClause(new TermQuery(new Term("Title", "dot")), Occur.SHOULD),
                                new BooleanClause(new TermQuery(new Term("Title", "net")), Occur.SHOULD)
                            }, Occur.SHOULD),
                            new BooleanClause(new BooleanQuery
                            {
                                Clauses =
                                {
                                    new BooleanClause(new TermQuery(new Term("Title", "dot")), Occur.MUST),
                                    new BooleanClause(new TermQuery(new Term("Title", "net")), Occur.MUST)
                                },
                                Boost = 4
                            }, Occur.SHOULD)
                        }, Occur.MUST)
                    }
                };

                // description
                yield return GetSimpleFieldQuery("Description");

                // tags
                yield return GetSimpleFieldQuery("Tags", 2);

                // authors
                yield return GetSimpleFieldQuery("Authors");

                // summary
                yield return GetSimpleFieldQuery("Summary");
            }
        }

        public static IEnumerable<object[]> MakesQueriesSupportingFieldAliasesData
        {
            get
            {
                yield return new object[] { "id", "Id" };
                yield return new object[] { "packageid", "Id" };
                yield return new object[] { "version", "Version" };
                yield return new object[] { "title", "Title" };
                yield return new object[] { "description", "Description" };
                yield return new object[] { "tag", "Tags" };
                yield return new object[] { "tags", "Tags" };
                yield return new object[] { "author", "Authors" };
                yield return new object[] { "authors", "Authors" };
                yield return new object[] { "summary", "Summary" };
            }
        }

        public static IEnumerable<object[]> MakesFilteredQueriesSupportingFieldAliasesData
        {
            get
            {
                yield return new object[] { "owner", "Owner" };
                yield return new object[] { "owners", "Owner" };
                yield return new object[] { "OWNER", "Owner" };
                yield return new object[] { "OWNERS", "Owner" };
            }
        }

        /// <summary>
        /// Some field queries have no special boosting or grouping rules.
        /// </summary>
        /// <param name="field">The field name.</param>
        /// <param name="boost">The expected boost on the field.</param>
        /// <returns>The parameters to test the query.</returns>
        private static object[] GetSimpleFieldQuery(string field, float boost = 1)
        {
            return new object[]
            {
                string.Format("{0}:dot {0}:bar", field),
                new BooleanQuery
                {
                    new BooleanClause(new BooleanQuery
                    {
                        Clauses =
                        {
                            new BooleanClause(new TermQuery(new Term(field, "dot")), Occur.SHOULD),
                            new BooleanClause(new TermQuery(new Term(field, "bar")), Occur.SHOULD)
                        },
                        Boost = boost
                    }, Occur.MUST)
                }
            };
        }
    }
}
