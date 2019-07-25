// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Linq;
using Xunit;

namespace NuGet.Services.AzureSearch.AuxiliaryFiles
{
    public class DownloadsByVersionDataFacts
    {
        public class Total : Facts
        {
            [Fact]
            public void StartsWithZero()
            {
                Assert.Equal(0, Target.Total);
            }

            [Fact]
            public void HasAllVersionCounts()
            {
                Target.SetDownloadCount(V1, 10);
                Target.SetDownloadCount(V2, 11);

                Assert.Equal(21, Target.Total);
            }
        }

        public class GetDownloadCount : Facts
        {
            [Fact]
            public void ReturnsDownloadCount()
            {
                Target.SetDownloadCount(V1, 10);

                Assert.Equal(10, Target.GetDownloadCount(V1));
            }

            [Fact]
            public void AllowsDifferentCase()
            {
                Target.SetDownloadCount(V1, 10);

                Assert.Equal(10, Target.GetDownloadCount(V1Upper));
            }

            [Fact]
            public void ReturnsZeroForMissingVersion()
            {
                Assert.Equal(0, Target.GetDownloadCount(V1));
            }
        }

        public class SetDownloadCount : Facts
        {
            [Fact]
            public void AllowsUpdatingDownloadCount()
            {
                Target.SetDownloadCount(V1, 10);
                Target.SetDownloadCount(V1, 1);

                Assert.Equal(1, Target.GetDownloadCount(V1));
                Assert.Equal(1, Target.Total);
            }

            [Fact]
            public void AllowsUpdatingDownloadCountWithDifferentCase()
            {
                Target.SetDownloadCount(V1, 10);
                Target.SetDownloadCount(V2, 5);
                Target.SetDownloadCount(V1Upper, 1);

                Assert.Equal(1, Target.GetDownloadCount(V1));
                Assert.Equal(6, Target.Total);
            }

            [Fact]
            public void ReplacesCaseOfVersionString()
            {
                Target.SetDownloadCount(V1, 10);
                Target.SetDownloadCount(V1Upper, 10);

                var pair = Assert.Single(Target);
                Assert.Equal(V1Upper, pair.Key);
                Assert.Equal(10, pair.Value);
            }

            [Fact]
            public void RemovesVersionWithZeroDownloads()
            {
                Target.SetDownloadCount(V1, 10);
                Target.SetDownloadCount(V1Upper, 0);

                Assert.Empty(Target);
            }

            [Fact]
            public void RejectsNegativeDownloadCount()
            {
                var ex = Assert.Throws<ArgumentOutOfRangeException>(() => Target.SetDownloadCount(V1, -1));
                Assert.Contains("The download count must not be negative.", ex.Message);
                Assert.Equal("downloads", ex.ParamName);
            }
        }

        public class EnumerableImplementation : Facts
        {
            [Fact]
            public void ReturnsVersionsInOrder()
            {
                Target.SetDownloadCount(V2, 2);
                Target.SetDownloadCount(V3, 3);
                Target.SetDownloadCount(V0, 0);
                Target.SetDownloadCount(V1Upper, 1);

                var items = Target.ToArray();

                Assert.Equal(
                    new[]
                    {
                        KeyValuePair.Create(V1Upper, 1L),
                        KeyValuePair.Create(V2, 2L),
                        KeyValuePair.Create(V3, 3L),
                    },
                    items);
            }
        }

        public abstract class Facts
        {
            public const string V0 = "0.0.0";
            public const string V1 = "1.0.0-alpha";
            public const string V1Upper = "1.0.0-ALPHA";
            public const string V2 = "2.0.0";
            public const string V3 = "3.0.0";

            public Facts()
            {
                Target = new DownloadByVersionData();
            }

            public DownloadByVersionData Target { get; }
        }
    }
}
