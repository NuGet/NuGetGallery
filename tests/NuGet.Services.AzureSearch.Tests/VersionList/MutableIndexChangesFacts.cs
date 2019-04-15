// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;
using System.Linq;
using NuGet.Versioning;
using Xunit;

namespace NuGet.Services.AzureSearch
{
    public class MutableIndexChangesFacts
    {
        public class Solidify
        {
            [Fact]
            public void MapsProperties()
            {
                var v1 = NuGetVersion.Parse("1.0.0");
                var changes = MutableIndexChanges.FromLatestIndexChanges(new Dictionary<SearchFilters, LatestIndexChanges>
                {
                    {
                        SearchFilters.Default,
                        new LatestIndexChanges(
                            SearchIndexChangeType.AddFirst,
                            new List<HijackIndexChange>
                            {
                                HijackIndexChange.UpdateMetadata(v1),
                                HijackIndexChange.SetLatestToTrue(v1),
                            })
                    },
                });

                var solid = changes.Solidify();

                Assert.Equal(new[] { SearchFilters.Default }, solid.Search.Keys.ToArray());
                Assert.Equal(SearchIndexChangeType.AddFirst, solid.Search[SearchFilters.Default]);
                Assert.False(solid.Hijack[v1].Delete);
                Assert.True(solid.Hijack[v1].UpdateMetadata);
                Assert.True(solid.Hijack[v1].LatestStableSemVer1);
                Assert.False(solid.Hijack[v1].LatestSemVer1);
                Assert.False(solid.Hijack[v1].LatestStableSemVer2);
                Assert.False(solid.Hijack[v1].LatestSemVer2);
            }
        }
    }
}
