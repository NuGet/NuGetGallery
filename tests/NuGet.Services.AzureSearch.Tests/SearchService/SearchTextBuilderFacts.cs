// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;
using System.Linq;
using Xunit;

namespace NuGet.Services.AzureSearch.SearchService
{
    public class SearchTextBuilderFacts
    {
        public class V2Search : FactsBase
        {
            [Theory]
            [MemberData(nameof(CommonAzureSearchQueryData))]
            public void GeneratesAzureSearchQuery(string input, string expected)
            {
                var parsed = _target.ParseV2Search(new V2SearchRequest { Query = input });

                var actual = _target.Build(parsed);

                Assert.Equal(expected, actual);
            }

            [Theory]
            [InlineData(false, "tokenizedPackageId:hello")]
            [InlineData(true, "packageId:hello")]
            public void WhenLuceneQuery_TreatsLeadingIdAsPackageId(bool luceneQuery, string expected)
            {
                var parsed = _target.ParseV2Search(new V2SearchRequest
                {
                    Query = "id:hello",
                    LuceneQuery = luceneQuery,
                });

                var actual = _target.Build(parsed);

                Assert.Equal(expected, actual);
            }

            [Theory]
            [MemberData(nameof(GenerateQueriesManyClauses))]
            public void ThrowsWhenQueryHasTooManyClauses(int nonFieldScopedTerms, int fieldScopedTerms, bool shouldThrow)
            {
                var request = new V2SearchRequest { Query = GenerateQuery(nonFieldScopedTerms, fieldScopedTerms) };
                var parsed = _target.ParseV2Search(request);

                if (shouldThrow)
                {
                    var e = Assert.Throws<InvalidSearchRequestException>(() => _target.Build(parsed));
                    Assert.Equal("A query can only have up to 1024 clauses.", e.Message);
                }
                else
                {
                    _target.ParseV2Search(request);
                }
            }

            [Theory]
            [MemberData(nameof(GenerateQueryWithTooBigTerm))]
            public void ThrowsWhenTermIsTooBig(string query)
            {
                var request = new V2SearchRequest { Query = query };
                var parsed = _target.ParseV2Search(request);

                var e = Assert.Throws<InvalidSearchRequestException>(() => _target.Build(parsed));

                Assert.Equal("Query terms cannot exceed 32768 bytes.", e.Message);
            }
        }

        public class V3Search : FactsBase
        {
            [Theory]
            [MemberData(nameof(CommonAzureSearchQueryData))]
            public void GeneratesAzureSearchQuery(string input, string expected)
            {
                var parsed = _target.ParseV3Search(new V3SearchRequest { Query = input });

                var actual = _target.Build(parsed);

                Assert.Equal(expected, actual);
            }

            [Theory]
            [MemberData(nameof(GenerateQueriesManyClauses))]
            public void ThrowsWhenQueryHasTooManyClauses(int nonFieldScopedTerms, int fieldScopedTerms, bool shouldThrow)
            {
                var request = new V3SearchRequest { Query = GenerateQuery(nonFieldScopedTerms, fieldScopedTerms) };
                var parsed = _target.ParseV3Search(request);

                if (shouldThrow)
                {
                    var e = Assert.Throws<InvalidSearchRequestException>(() => _target.Build(parsed));
                    Assert.Equal("A query can only have up to 1024 clauses.", e.Message);
                }
                else
                {
                    _target.ParseV3Search(request);
                }
            }

            [Theory]
            [MemberData(nameof(GenerateQueryWithTooBigTerm))]
            public void ThrowsWhenTermIsTooBig(string query)
            {
                var request = new V3SearchRequest { Query = query };
                var parsed = _target.ParseV3Search(request);

                var e = Assert.Throws<InvalidSearchRequestException>(() => _target.Build(parsed));

                Assert.Equal("Query terms cannot exceed 32768 bytes.", e.Message);
            }
        }

        public class Autocomplete : FactsBase
        {
            // TODO: This should use the autocomplete package id field
            // See https://github.com/NuGet/NuGetGallery/issues/6972
            [Theory]
            [InlineData("Test", "packageId:Test*")]
            [InlineData("Test ", "packageId:Test*")]
            [InlineData("title:test", "packageId:title\\:test*")]
            [InlineData("Hello world", "packageId:Hello\\ world*")]
            [InlineData("Hello world ", "packageId:Hello\\ world*")]
            public void PackageIdAutocomplete(string input, string expected)
            {
                var request = new AutocompleteRequest
                {
                    Query = input,
                    Type = AutocompleteRequestType.PackageIds
                };

                var actual = _target.Autocomplete(request);

                Assert.Equal(expected, actual);
            }

            [Theory]
            [InlineData("Test", "packageId:Test")]
            [InlineData("Hello world", @"packageId:""Hello world""")]
            public void PackageVersionsAutocomplete(string input, string expected)
            {
                var request = new AutocompleteRequest
                {
                    Query = input,
                    Type = AutocompleteRequestType.PackageVersions
                };

                var actual = _target.Autocomplete(request);

                Assert.Equal(expected, actual);
            }
        }

        public class FactsBase
        {
            protected readonly SearchTextBuilder _target;

            public FactsBase()
            {
                _target = new SearchTextBuilder();
            }

            public static IEnumerable<object[]> CommonAzureSearchQueryData()
            {
                // Map of inputs to expected output
                var data = new Dictionary<string, string>
                {
                    { "", "*" },
                    { " ", "*" },

                    { "id:test", "tokenizedPackageId:test" },
                    { "packageId:json", "packageId:json" },
                    { "version:1.0.0-test", "normalizedVersion:1.0.0\\-test" },
                    { "title:hello", "title:hello" },
                    { "description:hello", "description:hello" },
                    { "tag:hi", "tags:hi" },
                    { "tags:foo", "tags:foo" },
                    { "author:bob", "authors:bob" },
                    { "authors:billy", "authors:billy" },
                    { "summary:test", "summary:test" },
                    { "owner:goat", "owners:goat" },
                    { "owners:nugget", "owners:nugget" },

                    // The NuGet query fields are case insensitive
                    { "ID:TEST", "tokenizedPackageId:TEST" },
                    { "PACKAGEID:JSON", "packageId:JSON" },
                    { "VERSION:1.0.0-TEST", "normalizedVersion:1.0.0\\-TEST" },
                    { "TITLE:HELLO", "title:HELLO" },
                    { "DESCRIPTION:HELLO", "description:HELLO" },
                    { "TAG:HI", "tags:HI" },
                    { "TAGS:FOO", "tags:FOO" },
                    { "AUTHOR:BOB", "authors:BOB" },
                    { "AUTHORS:BILLY", "authors:BILLY" },
                    { "SUMMARY:TEST", "summary:TEST" },
                    { "OWNER:GOAT", "owners:GOAT" },
                    { "OWNERS:NUGGET", "owners:NUGGET" },
                    
                    // Unknown fields are ignored
                    { "fake:test", "*" },

                    // The version field is normalized, if possible
                    { "version:1.0.0.0", "normalizedVersion:1.0.0" },
                    { "version:1.0.0.0-test", "normalizedVersion:1.0.0\\-test" },
                    { "version:Thisisnotavalidversion", "normalizedVersion:Thisisnotavalidversion" },

                    // The tags field is split by delimiters
                    { "tag:a,b;c|d", "tags:(a b c d)" },
                    { "tags:a,b;c|d", "tags:(a b c d)" },

                    { "id:foo id:bar", "tokenizedPackageId:(foo bar)" },
                    { "packageId:foo packageId:bar", "packageId:(foo bar)" },
                    { "title:hello title:world", "title:(hello world)" },
                    { "description:I description:am", "description:(I am)" },
                    { "tag:a tag:sentient tags:being", "tags:(a sentient being)" },
                    { "author:a author:b authors:c", "authors:(a b c)" },
                    { "summary:d summary:e", "summary:(d e)" },
                    { "owner:billy owners:the owner:goat", "owners:(billy the goat)" },
                    { @"tag:a,b;c tags:d tags:""e f""", "tags:(a b c d e f)" },
                    
                    // If there are multiple terms, each field-scoped term must have at least one match
                    { "title:foo description:bar title:baz", "+title:(foo baz) +description:bar" },
                    { "title:foo bar", "+title:foo bar" },
                    { "title:foo unknown:bar", "title:foo" },

                    // If there are non-field-scoped terms and no field-scoped terms, at least of one the non-field-scoped terms is required.
                    { "foo bar", "foo bar" },
                    { "id packageId version title description tag author summary owner owners", "id packageId version title description tag author summary owner owners" },
                    { "ID PACKAGEID VERSION TITLE DESCRIPTION TAG AUTHOR SUMMARY OWNER OWNERS", "ID PACKAGEID VERSION TITLE DESCRIPTION TAG AUTHOR SUMMARY OWNER OWNERS" },
                    
                    // Quotes allow adjacent terms to be searched
                    { @"""foo bar""", @"""foo bar""" },
                    { @"""foo bar"" baz", @"""foo bar"" baz" },
                    { @"title:""foo bar""", @"title:""foo bar""" },
                    { @"title:""a b"" c title:d f", @"+title:(""a b"" d) c f" },
                    { @"title:"" a b    c   """, @"title:""a b    c""" },

                    // Dangling quotes are handled with best effort
                    { @"Tags:""windows", "tags:windows" },
                    { @"json Tags:""net"" Tags:""windows sdk", @"+tags:(net windows sdk) json" },
                    { @"json Tags:""net Tags:""windows sdk""", @"+tags:(net Tags\:) json windows sdk" },
                    { @"sdk Tags:""windows", "+tags:windows sdk" },
                    { @"Tags:""windows sdk", "tags:(windows sdk)" },
                    { @"Tags:""""windows""", "windows" },

                    // Empty quotes are ignored
                    { @"Tags:""""", @"*" },
                    { @"Tags:"" """, @"*" },
                    { @"Tags:""      """, @"*" },
                    { @"windows Tags:""      """, @"windows" },
                    { @"windows Tags:""      "" Tags:sdk", @"+tags:sdk windows" },

                    // Duplicate search terms on the same query field are folded
                    { "a a", "a" },
                    { "title:a title:a", "title:a" },
                    { "tag:a tags:a", "tags:a" },
                    { "tags:a,a", "tags:a" },

                    // Single word query terms are unquoted.
                    { @"""a""", "a" },
                    { @"title:""a""", "title:a" },

                    // Lucene keywords are removed unless quoted with other terms
                    { @"AND OR", @"*" },
                    { @"""AND"" ""OR""", @"*" },
                    { @"""AND OR""", @"""AND OR""" },
                    { @"hello AND world", @"hello world" },
                    { @"hello OR world", @"hello world" },
                    { @"title:""hello AND world""", @"title:""hello AND world""" },
                    { @"title:""hello OR world""", @"title:""hello OR world""" },

                    // Special characters are escaped
                    { @"title:+ description:""+""", @"+title:\+ +description:\+" },
                    { @"title:- description:""-""", @"+title:\- +description:\-" },
                    { @"title:& description:""&""", @"+title:\& +description:\&" },
                    { @"title:| description:""|""", @"+title:\| +description:\|" },
                    { @"title:! description:""!""", @"+title:\! +description:\!" },
                    { @"title:( description:""(""", @"+title:\( +description:\(" },
                    { @"title:) description:"")""", @"+title:\) +description:\)" },
                    { @"title:{ description:""{""", @"+title:\{ +description:\{" },
                    { @"title:} description:""}""", @"+title:\} +description:\}" },
                    { @"title:[ description:""[""", @"+title:\[ +description:\[" },
                    { @"title:] description:""]""", @"+title:\] +description:\]" },
                    { @"title:~ description:""~""", @"+title:\~ +description:\~" },
                    { @"title:* description:""*""", @"+title:\* +description:\*" },
                    { @"title:? description:""?""", @"+title:\? +description:\?" },
                    { @"title:\ description:""\""", @"+title:\\ +description:\\" },
                    { @"title:/ description:""/""", @"+title:\/ +description:\/" },
                    { @"title:"":""", @"title:\:" },

                    { @"+ - & | ! ( ) { } [ ] ~ * ? \ / "":""", @"\+ \- \& \| \! \( \) \{ \} \[ \] \~ \* \? \\ \/ \:" },

                    // Unicode surrogate pairs
                    { "A𠈓C", "A𠈓C" },
                    { "packageId:A𠈓C", "packageId:A𠈓C" },
                    { "A𠈓C packageId:A𠈓C A𠈓C packageId:A𠈓C hello packageId:hello", "+packageId:(A𠈓C hello) A𠈓C hello" },
                    { @"""A𠈓C"" packageId:""A𠈓C""", "+packageId:A𠈓C A𠈓C" },
                    { @"(𠈓) packageId:(𠈓)", @"+packageId:\(𠈓\) \(𠈓\)" },
                };

                foreach (var datum in data)
                {
                    yield return new object[] { datum.Key, datum.Value };
                }
            }

            public static IEnumerable<object[]> GenerateQueriesManyClauses()
            {
                object[] Setup(int nonFieldScopedTerms = 0, int fieldScopedTerms = 0, bool shouldThrow = false)
                {
                    return new object[] { nonFieldScopedTerms, fieldScopedTerms, shouldThrow };
                }

                // There must be less than 1025 clauses. Each non-field-scoped term count as a clause.
                yield return Setup(nonFieldScopedTerms: 1024, shouldThrow: false);
                yield return Setup(nonFieldScopedTerms: 1025, shouldThrow: true);

                // Each field-scoped terms count as a clause each, and the field-scope itself counts as a clause if it has more than one term.
                yield return Setup(fieldScopedTerms: 1023, shouldThrow: false);
                yield return Setup(fieldScopedTerms: 1024, shouldThrow: true);

                yield return Setup(nonFieldScopedTerms: 1023, fieldScopedTerms: 1, shouldThrow: false);
                yield return Setup(nonFieldScopedTerms: 1024, fieldScopedTerms: 1, shouldThrow: true);

                yield return Setup(nonFieldScopedTerms: 1021, fieldScopedTerms: 2, shouldThrow: false);
                yield return Setup(nonFieldScopedTerms: 1022, fieldScopedTerms: 2, shouldThrow: true);
            }

            protected string GenerateQuery(int nonFieldScopedTerms, int fieldScopedTerms)
            {
                List<string> GenerateTerms(int count)
                {
                    return Enumerable.Range(0, count).Select(i => i.ToString()).ToList();
                }

                var nonFieldScoped = GenerateTerms(nonFieldScopedTerms);
                var result = string.Join(" ", nonFieldScoped);

                if (fieldScopedTerms == 0)
                {
                    return result;
                }

                if (nonFieldScopedTerms > 0)
                {
                    result += " ";
                }

                var fieldScoped = GenerateTerms(fieldScopedTerms);
                result += $"packageId:{string.Join(" packageId:", fieldScoped)}";

                return result;
            }

            public static IEnumerable<object[]> GenerateQueryWithTooBigTerm()
            {
                yield return new object[] { new string('a', 32 * 1024 + 1) };
            }
        }
    }
}
