// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Linq;
using System.Text;
using Xunit;

namespace NuGet.Services.AzureSearch.AuxiliaryFiles
{
    public class DownloadDataFacts
    {
        public class GetDownloadCountById : Facts
        {
            [Fact]
            public void ReturnsZeroForUnknownId()
            {
                Assert.Equal(0, Target.GetDownloadCount(IdA));
            }

            [Fact]
            public void ReturnsTotalForId()
            {
                Target.SetDownloadCount(IdA, V1, 1);
                Target.SetDownloadCount(IdB, V2, 5);
                Target.SetDownloadCount(IdA, V3, 10);

                Assert.Equal(11, Target.GetDownloadCount(IdA));
            }
        }

        public class GetDownloadCountByIdAndVersion : Facts
        {
            [Fact]
            public void ReturnsZeroForUnknownIdAndVersion()
            {
                Assert.Equal(0, Target.GetDownloadCount(IdA, V1));
            }

            [Fact]
            public void ReturnsZeroForUnknownVersion()
            {
                Target.SetDownloadCount(IdA, V1, 1);

                Assert.Equal(0, Target.GetDownloadCount(IdA, V2));
            }

            [Fact]
            public void ReturnsDownloadsForVersion()
            {
                Target.SetDownloadCount(IdA, V1, 1);

                Assert.Equal(1, Target.GetDownloadCount(IdA));
            }
        }

        public class SetDownloadCount : Facts
        {
            [Fact]
            public void AllowsUpdatingDownloadCount()
            {
                Target.SetDownloadCount(IdA, V1, 10);
                Target.SetDownloadCount(IdA, V1, 1);

                Assert.Equal(1, Target.GetDownloadCount(IdA, V1));
                Assert.Equal(1, Target.GetDownloadCount(IdA));
            }

            [Fact]
            public void AllowsUpdatingDownloadCountWithDifferentCase()
            {
                Target.SetDownloadCount(IdA, V1, 10);
                Target.SetDownloadCount(IdA, V2, 5);
                Target.SetDownloadCount(IdAUpper, V1, 1);

                Assert.Equal(1, Target.GetDownloadCount(IdA, V1));
                Assert.Equal(6, Target.GetDownloadCount(IdA));
            }

            [Fact]
            public void ReplacesCaseOfVersionString()
            {
                Target.SetDownloadCount(IdA, V1, 10);
                Target.SetDownloadCount(IdAUpper, V1, 10);

                var pair = Assert.Single(Target);
                Assert.Equal(IdAUpper, pair.Key);
                Assert.Equal(10, pair.Value.Total);
            }

            [Fact]
            public void RemovesVersionWithZeroDownloads()
            {
                Target.SetDownloadCount(IdA, V1, 10);
                Target.SetDownloadCount(IdA, V1Upper, 0);

                Assert.Empty(Target);
            }

            [Fact]
            public void RejectsNegativeDownloadCount()
            {
                var ex = Assert.Throws<ArgumentOutOfRangeException>(() => Target.SetDownloadCount(IdA, V1, -1));
                Assert.Contains("The download count must not be negative.", ex.Message);
                Assert.Equal("downloads", ex.ParamName);
            }

            [Fact]
            public void DedupesVersionStrings()
            {
                var v1A = new StringBuilder(V1).Append(string.Empty).ToString();
                var v1B = new StringBuilder(V1).Append(string.Empty).ToString();
                Assert.NotSame(v1A, v1B);

                Target.SetDownloadCount(IdA, v1A, 1);
                Target.SetDownloadCount(IdB, v1B, 10);

                var records = Target
                    .SelectMany(i => i.Value.Select(v => new { Id = i.Key, Version = v.Key, Downloads = v.Key }))
                    .ToList();
                Assert.Equal(2, records.Count);
                Assert.Same(records[0].Version, records[1].Version);
            }
        }

        public abstract class Facts
        {
            public const string V0 = "0.0.0";
            public const string V1 = "1.0.0-alpha";
            public const string V1Upper = "1.0.0-ALPHA";
            public const string V2 = "2.0.0";
            public const string V3 = "3.0.0";

            public const string IdA = "NuGet.Frameworks";
            public const string IdAUpper = "NUGET.FRAMEWORKS";
            public const string IdB = "NuGet.Versioning";

            public Facts()
            {
                Target = new DownloadData();
            }

            public DownloadData Target { get; }
        }
    }
}
