// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using Xunit;

namespace NuGet.Services.Entities.Tests
{
    public class StagingEnumFacts
    {
        [Fact]
        public void PersistedValuesAreImmutable()
        {
            AssertValues(new Dictionary<StagingArtifactStatus, int>
            {
                { StagingArtifactStatus.Validating, 0 },
                { StagingArtifactStatus.Ready, 1 },
                { StagingArtifactStatus.ValidationFailed, 2 },
                { StagingArtifactStatus.Promoting, 3 },
                { StagingArtifactStatus.PromotionFailed, 4 },
            });
            AssertValues(new Dictionary<StagingPromotionHistoryScope, int>
            {
                { StagingPromotionHistoryScope.Individual, 0 },
                { StagingPromotionHistoryScope.Group, 1 },
            });
            AssertValues(new Dictionary<StagingPromotionHistoryStatus, int>
            {
                { StagingPromotionHistoryStatus.InProgress, 0 },
                { StagingPromotionHistoryStatus.Succeeded, 1 },
                { StagingPromotionHistoryStatus.PartiallySucceeded, 2 },
                { StagingPromotionHistoryStatus.Abandoned, 3 },
            });
            AssertValues(new Dictionary<StagingPromotionArtifactHistoryKind, int>
            {
                { StagingPromotionArtifactHistoryKind.Package, 0 },
                { StagingPromotionArtifactHistoryKind.Symbol, 1 },
            });
            AssertValues(new Dictionary<StagingPromotionArtifactHistoryStatus, int>
            {
                { StagingPromotionArtifactHistoryStatus.Pending, 0 },
                { StagingPromotionArtifactHistoryStatus.Processing, 1 },
                { StagingPromotionArtifactHistoryStatus.Succeeded, 2 },
                { StagingPromotionArtifactHistoryStatus.PromotionFailed, 3 },
                { StagingPromotionArtifactHistoryStatus.Abandoned, 4 },
            });
        }

        private static void AssertValues<T>(IReadOnlyDictionary<T, int> expected)
            where T : struct
        {
            Assert.Equal(
                expected.Keys.OrderBy(x => x),
                Enum.GetValues(typeof(T)).Cast<T>().OrderBy(x => x));

            foreach (var pair in expected)
            {
                Assert.Equal(pair.Value, Convert.ToInt32(pair.Key));
            }
        }
    }
}
