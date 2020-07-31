// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Text;
using Xunit;

namespace NuGet.Services.AzureSearch.AuxiliaryFiles
{
    public class StringCacheFacts
    {
        public class Dedupe : Facts
        {
            [Fact]
            public void DedupesStrings()
            {
                var a1 = MakeString("aaa");
                var a2 = MakeString("aaa");
                var a3 = Target.Dedupe(a1);

                var a4 = Target.Dedupe(a2);

                Assert.NotSame(a1, a2);
                Assert.Same(a1, a3);
                Assert.Same(a1, a4);
            }
            [Fact]
            public void DedupesCaseSensitively()
            {
                var a1 = MakeString("aaa");
                var a2 = MakeString("AAA");

                var a3 = Target.Dedupe(a2);

                Assert.NotEqual(a1, a3);
            }
        }

        public class StringCount : Facts
        {
            [Fact]
            public void CountsUniqueStrings()
            {
                Target.Dedupe(MakeString("aaa"));
                Target.Dedupe(MakeString("bbb"));
                Target.Dedupe(MakeString("aaa"));
                Target.Dedupe(MakeString("AAA"));

                Assert.Equal(3, Target.StringCount);
            }
        }

        public class RequestCount : Facts
        {
            [Fact]
            public void CountsNumberOfCalls()
            {
                Target.Dedupe(MakeString("aaa"));
                Target.Dedupe(MakeString("bbb"));
                Target.Dedupe(MakeString("aaa"));
                Target.Dedupe(MakeString("AAA"));

                Assert.Equal(4, Target.RequestCount);
            }
        }

        public class HitCount : Facts
        {
            [Fact]
            public void CountsNumberOfDedupedStrings()
            {
                Target.Dedupe(MakeString("aaa"));
                Target.Dedupe(MakeString("bbb"));
                Target.Dedupe(MakeString("aaa"));
                Target.Dedupe(MakeString("AAA"));

                Assert.Equal(1, Target.HitCount);
            }
        }

        public class CharCount : Facts
        {
            [Fact]
            public void CountsNumberOfCharactersInDedupedStrings()
            {
                Target.Dedupe(MakeString("a"));
                Target.Dedupe(MakeString("bb"));
                Target.Dedupe(MakeString("ccc"));
                Target.Dedupe(MakeString("dddd"));
                Target.Dedupe(MakeString("a"));
                Target.Dedupe(MakeString("bb"));
                Target.Dedupe(MakeString("ccc"));
                Target.Dedupe(MakeString("dddd"));

                Assert.Equal(10, Target.CharCount);
            }
        }

        public class ResetCounts : Facts
        {
            [Fact]
            public void CountsNumberOfDedupedStrings()
            {
                Target.Dedupe(MakeString("aaa"));
                Target.Dedupe(MakeString("bbb"));
                Target.Dedupe(MakeString("aaa"));
                Target.Dedupe(MakeString("AAA"));

                Target.ResetCounts();

                Assert.Equal(3, Target.StringCount);
                Assert.Equal(0, Target.RequestCount);
                Assert.Equal(0, Target.HitCount);
                Assert.Equal(9, Target.CharCount);
            }
        }

        public class Facts
        {
            public Facts()
            {
                Target = new StringCache();
            }

            public StringCache Target { get; }

            /// <summary>
            /// Make sure there's no funny compile time string de-duping.
            /// </summary>
            public string MakeString(string input)
            {
                var sb = new StringBuilder();
                foreach (var c in input)
                {
                    sb.Append(c);
                }

                return sb.ToString();
            }
        }
    }
}
