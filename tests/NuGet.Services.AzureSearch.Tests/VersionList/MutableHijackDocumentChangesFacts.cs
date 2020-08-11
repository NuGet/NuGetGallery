// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using Xunit;

namespace NuGet.Services.AzureSearch
{
    public class MutableHijackDocumentChangesFacts
    {
        public class Solidify
        {
            [Fact]
            public void DefaultsNullToFalse()
            {
                var output = new MutableHijackDocumentChanges().Solidify();

                Assert.False(output.Delete);
                Assert.False(output.UpdateMetadata);
                Assert.False(output.LatestStableSemVer1);
                Assert.False(output.LatestSemVer1);
                Assert.False(output.LatestStableSemVer2);
                Assert.False(output.LatestSemVer2);
            }

            [Fact]
            public void MapsDeleteDocument()
            {
                var output = new MutableHijackDocumentChanges(
                    delete: true,
                    updateMetadata: false,
                    latestStableSemVer1: null,
                    latestSemVer1: null,
                    latestStableSemVer2: null,
                    latestSemVer2: null).Solidify();

                Assert.True(output.Delete);
                Assert.False(output.UpdateMetadata);
                Assert.False(output.LatestStableSemVer1);
                Assert.False(output.LatestSemVer1);
                Assert.False(output.LatestStableSemVer2);
                Assert.False(output.LatestSemVer2);
            }

            [Fact]
            public void MapsUpdateLatestDocument()
            {
                var output = new MutableHijackDocumentChanges(
                    delete: false,
                    updateMetadata: true,
                    latestStableSemVer1: true,
                    latestSemVer1: true,
                    latestStableSemVer2: true,
                    latestSemVer2: true).Solidify();

                Assert.False(output.Delete);
                Assert.True(output.UpdateMetadata);
                Assert.True(output.LatestStableSemVer1);
                Assert.True(output.LatestSemVer1);
                Assert.True(output.LatestStableSemVer2);
                Assert.True(output.LatestSemVer2);
            }
        }

        public class ApplyChange
        {
            [Fact]
            public void DoesNotAllowUpdateMetadataToDeleteTransition()
            {
                var doc = new MutableHijackDocumentChanges();
                doc.ApplyChange(SearchFilters.Default, HijackIndexChangeType.UpdateMetadata);

                var ex = Assert.Throws<InvalidOperationException>(
                    () => doc.ApplyChange(SearchFilters.Default, HijackIndexChangeType.Delete));
                Assert.Equal(
                    "The hijack document has already been set to update metadata.",
                    ex.Message);
            }

            [Fact]
            public void DoesNotAllowDeleteToUpdateMetadataTransition()
            {
                var doc = new MutableHijackDocumentChanges();
                doc.ApplyChange(SearchFilters.Default, HijackIndexChangeType.Delete);

                var ex = Assert.Throws<InvalidOperationException>(
                    () => doc.ApplyChange(SearchFilters.Default, HijackIndexChangeType.UpdateMetadata));
                Assert.Equal(
                    "The hijack document has already been set to delete so metadata can't be updated.",
                    ex.Message);
            }

            [Theory]
            [MemberData(nameof(ChangeAndLatest))]
            public void SetLatestWithDefaultSearchFilters(HijackIndexChangeType change, bool latest)
            {
                var doc = new MutableHijackDocumentChanges();

                doc.ApplyChange(SearchFilters.Default, change);

                Assert.Equal(latest, doc.LatestStableSemVer1);
                Assert.Null(doc.LatestSemVer1);
                Assert.Null(doc.LatestStableSemVer2);
                Assert.Null(doc.LatestSemVer2);
            }

            [Theory]
            [MemberData(nameof(ChangeAndLatest))]
            public void SetLatestWithIncludePrereleaseSearchFilters(HijackIndexChangeType change, bool latest)
            {
                var doc = new MutableHijackDocumentChanges();

                doc.ApplyChange(SearchFilters.IncludePrerelease, change);

                Assert.Null(doc.LatestStableSemVer1);
                Assert.Equal(latest, doc.LatestSemVer1);
                Assert.Null(doc.LatestStableSemVer2);
                Assert.Null(doc.LatestSemVer2);
            }

            [Theory]
            [MemberData(nameof(ChangeAndLatest))]
            public void SetLatestWithIncludeSemVer2SearchFilters(HijackIndexChangeType change, bool latest)
            {
                var doc = new MutableHijackDocumentChanges();

                doc.ApplyChange(SearchFilters.IncludeSemVer2, change);

                Assert.Null(doc.LatestStableSemVer1);
                Assert.Null(doc.LatestSemVer1);
                Assert.Equal(latest, doc.LatestStableSemVer2);
                Assert.Null(doc.LatestSemVer2);
            }

            [Theory]
            [MemberData(nameof(ChangeAndLatest))]
            public void SetLatestWithIncludePrereleaseAndSemVer2SearchFilters(HijackIndexChangeType change, bool latest)
            {
                var doc = new MutableHijackDocumentChanges();

                doc.ApplyChange(SearchFilters.IncludePrereleaseAndSemVer2, change);

                Assert.Null(doc.LatestStableSemVer1);
                Assert.Null(doc.LatestSemVer1);
                Assert.Null(doc.LatestStableSemVer2);
                Assert.Equal(latest, doc.LatestSemVer2);
            }

            public static IEnumerable<object[]> ChangeAndLatest => new[]
            {
                new object[] { HijackIndexChangeType.SetLatestToTrue, true },
                new object[] { HijackIndexChangeType.SetLatestToFalse, false },
            };

            [Fact]
            public void DeleteTransitionClearsLatest()
            {
                var doc = new MutableHijackDocumentChanges();
                doc.ApplyChange(SearchFilters.Default, HijackIndexChangeType.SetLatestToTrue);
                doc.ApplyChange(SearchFilters.IncludePrerelease, HijackIndexChangeType.SetLatestToTrue);
                doc.ApplyChange(SearchFilters.IncludeSemVer2, HijackIndexChangeType.SetLatestToTrue);
                doc.ApplyChange(SearchFilters.IncludePrereleaseAndSemVer2, HijackIndexChangeType.SetLatestToTrue);

                doc.ApplyChange(SearchFilters.Default, HijackIndexChangeType.Delete);

                Assert.Null(doc.LatestStableSemVer1);
                Assert.Null(doc.LatestSemVer1);
                Assert.Null(doc.LatestStableSemVer2);
                Assert.Null(doc.LatestSemVer2);
            }
        }
    }
}
