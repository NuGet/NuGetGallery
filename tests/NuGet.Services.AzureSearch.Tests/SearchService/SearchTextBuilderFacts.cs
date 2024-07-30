// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;
using System.Linq;
using Microsoft.Extensions.Options;
using Moq;
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

                Assert.Equal(expected, actual.Value);
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

                Assert.Equal(expected, actual.Value);
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

            [Theory]
            [InlineData("foo", false, false, "+foo* packageId:foo^10 -owners:TestUserA -owners:TestUserB")]
            [InlineData("", false, false, "packageId:/.*/ -owners:TestUserA -owners:TestUserB")]
            [InlineData("foo", true, false, "+foo* packageId:foo^10")]
            [InlineData("", true, false, "*")]
            [InlineData("foo", false, true, "+foo* packageId:foo^10")]
            [InlineData("", false, true, "*")]
            [InlineData("foo", true, true, "+foo* packageId:foo^10")]
            [InlineData("", true, true, "*")]
            public void CanExcludeTestData(string query, bool ignoreFilter, bool includeTestData, string expected)
            {
                _config.TestOwners = new List<string> { "TestUserA", "TestUserB" };
                var request = new V2SearchRequest
                {
                    Query = query,
                    IgnoreFilter = ignoreFilter,
                    IncludeTestData = includeTestData,
                };
                var parsed = _target.ParseV2Search(request);

                var actual = _target.Build(parsed);

                Assert.Equal(expected, actual.Value);
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

                Assert.Equal(expected, actual.Value);
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

            [Theory]
            [InlineData("foo", false, "+foo* packageId:foo^10 -owners:TestUserA -owners:TestUserB")]
            [InlineData("", false, "packageId:/.*/ -owners:TestUserA -owners:TestUserB")]
            [InlineData("foo", true, "+foo* packageId:foo^10")]
            [InlineData("", true, "*")]
            public void CanExcludeTestData(string query, bool includeTestData, string expected)
            {
                _config.TestOwners = new List<string> { "TestUserA", "TestUserB" };
                var request = new V3SearchRequest
                {
                    Query = query,
                    IncludeTestData = includeTestData,
                };
                var parsed = _target.ParseV3Search(request);

                var actual = _target.Build(parsed);

                Assert.Equal(expected, actual.Value);
            }
        }

        public class Autocomplete : FactsBase
        {
            [Theory]
            [InlineData("Test", "packageId:Test* +tokenizedPackageId:Test* packageId:Test^1000")]
            [InlineData("Test ", "packageId:Test* +tokenizedPackageId:Test* packageId:Test^1000")]
            [InlineData("title:test", "packageId:title\\:test* +tokenizedPackageId:title\\:test*")]
            [InlineData("Hello world", "packageId:Hello\\ world* +tokenizedPackageId:Hello\\ world*")]
            [InlineData("Hello world ", "packageId:Hello\\ world* +tokenizedPackageId:Hello\\ world*")]
            [InlineData("Hello.world", "packageId:Hello.world* +tokenizedPackageId:Hello* +tokenizedPackageId:world* packageId:Hello.world^1000")]
            [InlineData("Foo.BarBaz", "packageId:Foo.BarBaz* +tokenizedPackageId:Foo* +tokenizedPackageId:BarBaz* packageId:Foo.BarBaz^1000")]
            public void PackageIdsAutocomplete(string input, string expected)
            {
                var request = new AutocompleteRequest
                {
                    Query = input,
                    Type = AutocompleteRequestType.PackageIds
                };

                var actual = _target.Autocomplete(request);

                Assert.Equal(expected, actual.Value);
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

                Assert.Equal(expected, actual.Value);
            }

            [Theory]
            [InlineData("Test", false, "packageId:Test* +tokenizedPackageId:Test* packageId:Test^1000 -owners:TestUserA -owners:TestUserB")]
            [InlineData("", false, "packageId:/.*/ -owners:TestUserA -owners:TestUserB")]
            [InlineData("Test", true, "packageId:Test* +tokenizedPackageId:Test* packageId:Test^1000")]
            [InlineData("", true, "*")]
            public void PackageIdsCanExcludeTestData(string query, bool includeTestData, string expected)
            {
                _config.TestOwners = new List<string> { "TestUserA", "TestUserB" };
                var request = new AutocompleteRequest
                {
                    Query = query,
                    Type = AutocompleteRequestType.PackageIds,
                    IncludeTestData = includeTestData,
                };

                var actual = _target.Autocomplete(request);

                Assert.Equal(expected, actual.Value);
            }

            [Theory]
            [InlineData("Test", false, "packageId:Test -owners:TestUserA -owners:TestUserB")]
            [InlineData("Test", true, "packageId:Test")]
            public void PackageVersionsCanExcludeTestData(string query, bool includeTestData, string expected)
            {
                _config.TestOwners = new List<string> { "TestUserA", "TestUserB" };
                var request = new AutocompleteRequest
                {
                    Query = query,
                    Type = AutocompleteRequestType.PackageVersions,
                    IncludeTestData = includeTestData,
                };

                var actual = _target.Autocomplete(request);

                Assert.Equal(expected, actual.Value);
            }
        }

        public class FactsBase
        {
            protected readonly SearchTextBuilder _target;
            protected readonly SearchServiceConfiguration _config;

            public FactsBase()
            {
                _config = new SearchServiceConfiguration();
                var options = new Mock<IOptionsSnapshot<SearchServiceConfiguration>>();
                options.Setup(o => o.Value).Returns(_config);

                _target = new SearchTextBuilder(options.Object);
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
                    { "foo:a bar:b", "*" },

                    // The version field is normalized, if possible
                    { "version:1.0.0.0", "normalizedVersion:1.0.0" },
                    { "version:1.0.0.0-test", "normalizedVersion:1.0.0\\-test" },
                    { "version:Thisisnotavalidversion", "normalizedVersion:Thisisnotavalidversion" },

                    // The tags field is split by delimiters
                    { "tag:a,b;c|d", "tags:(a b c d)" },
                    { "tags:a,b;c|d", "tags:(a b c d)" },
                    { "tags:,;|", "*" },

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
                    { "title:foo bar", "+title:foo +bar*" },
                    { "title:foo unknown:bar", "title:foo" },

                    // If there are non-field-scoped terms and no field-scoped terms, at least of one the non-field-scoped terms is required.
                    // If there are no field-scoped terms, results that prefix match the last term are boosted
                    // Results that match all terms are boosted.
                    // Results that match all terms after tokenization are boosted.
                    { "foo", "+foo* packageId:foo^10" },
                    { "foobar", "+foobar* foobar^2 packageId:foobar^10" },
                    { "foo bar", "+foo +bar*" },
                    { "foo.bar baz.qux", "+foo +bar +baz +qux*" },
                    { "id packageId VERSION Title description tag author summary owner owners",
                        "+id +(packageid (+package +Id)) packageid^2 +version version^2 +title title^2 +description description^2 +tag +author author^2 +summary summary^2 +owner owner^2 +owners* owners^2" },

                    // If there is a single non-field-scoped term that is a valid package ID and has separator
                    // characters, boost results that match all tokens, boost results that prefix match the last token,
                    // and mega boost the exact match.
                    { "foo.bar", "+foo +bar* packageId:foo.bar*^100 packageId:foo.bar^1000" },
                    { "foo_bar", "+foo +bar* packageId:foo_bar*^100 packageId:foo_bar^1000" },
                    { "foo-bar", @"+foo +bar* packageId:foo\-bar*^100 packageId:foo\-bar^1000" },
                    { "  foo.bar.Baz   ", "+foo +bar +baz* packageId:foo.bar.Baz*^100 packageId:foo.bar.Baz^1000" },
                    { @"""foo.bar""", @"+foo +bar* packageId:foo.bar*^100 packageId:foo.bar^1000" },
                    { @"""foo-bar""", @"+foo +bar* packageId:foo\-bar*^100 packageId:foo\-bar^1000" },
                    { @"""foo_bar""", @"+foo +bar* packageId:foo_bar*^100 packageId:foo_bar^1000" },

                    // The last instance of a token should be prefixed
                    { "a.b.a", "+b +a* packageId:a.b.a*^100 packageId:a.b.a^1000" },

                    // Boost results that match all tokens from unscoped terms in the query.
                    { "foo.bar buzz", "+foo +bar +buzz* buzz^2" },
                    { "foo_bar buzz", "+foo +bar +buzz* buzz^2" },
                    { "foo-bar buzz", "+foo +bar +buzz* buzz^2" },
                    { "foo,bar, buzz", "+foo +bar +buzz* buzz^2" },
                    { "fooBar", "+(foobar* (+foo +Bar)) foobar^2 packageId:fooBar^10" },
                    { "fooBar.", "+(foobar* (+foo +Bar)) foobar^2 packageId:fooBar.*^100" },
                    { "fooBar buzz", "+(foobar (+foo +Bar)) foobar^2 +buzz* buzz^2" },
                    { "foo5", "+(foo5* (+foo +5)) foo5^2 packageId:foo5^10" },
                    { "foo5 buzz", "+(foo5 (+foo +5)) foo5^2 +buzz* buzz^2" },
                    { "FOO5 buzz", "+(foo5 (+FOO +5)) foo5^2 +buzz* buzz^2" },
                    { "5FOO buzz", "+(5foo (+5 +FOO)) 5foo^2 +buzz* buzz^2" },
                    { "foo5foo", "+(foo5foo* (+foo +5)) foo5foo^2 packageId:foo5foo^10" },
                    { "FOO5FOO", "+(foo5foo* (+FOO +5)) foo5foo^2 packageId:FOO5FOO^10" },
                    { "fooFoo", "+foofoo* foofoo^2 packageId:fooFoo^10" },
                    { "FOOFoo", "+foofoo* foofoo^2 packageId:FOOFoo^10" },

                    // Phrases are supported in queries
                    { @"""foo bar""", @"+foo\ bar* ""foo bar""^2" },
                    { @"""foo bar"" baz", @"+""foo bar"" ""foo bar""^2 +baz*" },
                    { @"title:""foo bar""", @"title:""foo bar""" },
                    { @"title:""a b"" c title:d f", @"+title:(""a b"" d) +c +f*" },
                    { @"title:"" a b    c   """, @"title:""a b    c""" },

                    // Dangling quotes are handled with best effort
                    { @"Tags:""windows", "tags:windows" },
                    { @"json Tags:""net"" Tags:""windows sdk", @"+tags:(net windows sdk) +json* json^2" },
                    { @"json Tags:""net Tags:""windows sdk""", @"+tags:(net Tags\:) +json json^2 +windows windows^2 +sdk*" },
                    { @"sdk Tags:""windows", "+tags:windows +sdk*" },
                    { @"Tags:""windows sdk", "tags:(windows sdk)" },
                    { @"Tags:""""windows""", "+windows* windows^2 packageId:windows^10" },

                    // Empty quotes are ignored
                    { @"Tags:""""", @"*" },
                    { @"Tags:"" """, @"*" },
                    { @"Tags:""      """, @"*" },
                    { @"windows Tags:""      """, @"+windows* windows^2 packageId:windows^10" },
                    { @"windows Tags:""      "" Tags:sdk", @"+tags:sdk +windows* windows^2" },

                    // Duplicate search terms on the same query field are folded
                    { "a a", "+a* packageId:a^10" },
                    { "title:a title:a", "title:a" },
                    { "tag:a tags:a", "tags:a" },
                    { "tags:a,a", "tags:a" },

                    // Single word query terms are unquoted.
                    { @"""a""", "+a* packageId:a^10" },
                    { @"title:""a""", "title:a" },

                    // Lucene keywords are removed unless quoted with other terms
                    { @"AND OR NOT", @"*" },
                    { @"and or not", @"*" },
                    { @"""AND""", @"*" },
                    { @"""OR""", @"*" },
                    { @"""NOT""", @"*" },
                    { @"""AND"" ""OR"" ""NOT""", @"*" },
                    { @"""AND OR NOT""", @"+and\ or\ not* ""and or not""^2" },
                    { @"""AND OR""", @"+and\ or* ""and or""^2" },
                    { @"""OR NOT""", @"+or\ not* ""or not""^2" },
                    { @"""AND NOT""", @"+and\ not* ""and not""^2" },
                    { @""" AND""", @"+and* and^2" },
                    { @""" OR """, @"+or* or^2" },
                    { @"""NOT """, @"+not* not^2" },
                    { @"hello AND world", @"+hello hello^2 +world* world^2" },
                    { @"hello OR world", @"+hello hello^2 +world* world^2" },
                    { @"hello NOT world", @"+hello hello^2 +world* world^2" },
                    { @"title:""hello AND world""", @"title:""hello AND world""" },
                    { @"title:""hello OR world""", @"title:""hello OR world""" },
                    { @"title:""hello NOT world""", @"title:""hello NOT world""" },

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

                    { @"+ - & | ! ( ) { } [ ] ~ * ? \ / "":""", @"\+ \- \& \| \! \( \) \{ \} \[ \] \~ \* \? \\ \/ \:"},

                    // Unicode surrogate pairs
                    { "A𠈓C", "+a𠈓c* a𠈓c^2" },
                    { "packageId:A𠈓C", "packageId:A𠈓C" },
                    { @"""A𠈓C"" packageId:""A𠈓C""", "+packageId:A𠈓C +a𠈓c* a𠈓c^2" },
                    { @"(𠈓) packageId:(𠈓)", @"+packageId:\(𠈓\) +\(𠈓\)* \(𠈓\)^2" },
                    { "A𠈓C packageId:A𠈓C A𠈓C packageId:A𠈓C hello packageId:hello", "+packageId:(A𠈓C hello) +a𠈓c a𠈓c^2 +hello* hello^2" },
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
