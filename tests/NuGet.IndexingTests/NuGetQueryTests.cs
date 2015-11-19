using System.Collections.Generic;
using Lucene.Net.Index;
using Lucene.Net.Search;
using NuGet.Indexing;
using Xunit;

namespace NuGet.IndexingTests
{
    public class NuGetQueryTests
    {
        [Theory, MemberData("MakeQueryTheoryData")]
        public void MakeQueryTheory(string input, Query expected)
        {
            var actual = NuGetQuery.MakeQuery(input);
            Assert.Equal(expected, actual);
        }

        public static IEnumerable<object[]> MakeQueryTheoryData
        {
            get
            {
                yield return new object[]
                {
                    "id:Aa id:Bb",
                    new BooleanQuery
                    {
                        new BooleanClause(new BooleanQuery
                        {
                            new BooleanClause(new BooleanQuery
                            {
                                new BooleanClause(new TermQuery(new Term("Id", "aa")), Occur.SHOULD),
                                new BooleanClause(new TermQuery(new Term("Id", "bb")), Occur.SHOULD)
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

                yield return new object[]
                {
                    "Version:1.02.003 Version:04.5",
                    new BooleanQuery
                    {
                        new BooleanClause(new BooleanQuery
                        {
                            new BooleanClause(new TermQuery(new Term("Owner", "abc-def")), Occur.SHOULD),
                            new BooleanClause(new TermQuery(new Term("Owner", "ghi")), Occur.SHOULD)
                        }, Occur.MUST)
                    }
                };

                yield return new object[]
                {
                    "OWNER:\"ABC-DEF\" OWNER:GHI",
                    new BooleanQuery
                    {
                        new BooleanClause(new BooleanQuery
                        {
                            new BooleanClause(new TermQuery(new Term("Owner", "abc-def")), Occur.SHOULD),
                            new BooleanClause(new TermQuery(new Term("Owner", "ghi")), Occur.SHOULD)
                        }, Occur.MUST)
                    }
                };
            }
        }
    }
}
