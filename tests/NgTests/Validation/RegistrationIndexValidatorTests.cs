// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using NuGet.Services.Metadata.Catalog.Monitoring;
using Xunit;

namespace NgTests.Validation
{
    public class RegistrationIndexValidatorTests
    {
        private static IEnumerable<object[]> ValidatorTestData<T>(Func<IRegistrationIndexValidatorTestData, IEnumerable<Tuple<T, T>>> getPairs)
        {
            foreach (var testData in ValidatorTestUtility.GetImplementations<IRegistrationIndexValidatorTestData>())
            {
                var validator = testData.CreateValidator();

                foreach (var pair in getPairs(testData))
                {
                    yield return new object[]
                    {
                        validator,
                        pair.Item1,
                        pair.Item2
                    };
                }
            }
        }

        private static IEnumerable<object[]> ValidatorSpecialTestData<T>(Func<IRegistrationIndexValidatorTestData, IEnumerable<Tuple<T, T, bool>>> getPairs)
        {
            foreach (var testData in ValidatorTestUtility.GetImplementations<IRegistrationIndexValidatorTestData>())
            {
                var validator = testData.CreateValidator();

                foreach (var pair in getPairs(testData))
                {
                    yield return new object[]
                    {
                        validator,
                        pair.Item1,
                        pair.Item2,
                        pair.Item3
                    };
                }
            }
        }

        public class TheCompareIndexMethod
        {
            public static IEnumerable<object[]> ValidatorEqualIndexTestData => ValidatorTestData(t => ValidatorTestUtility.GetEqualPairs(t.CreateIndexes));

            public static IEnumerable<object[]> ValidatorUnequalIndexTestData => ValidatorTestData(t => ValidatorTestUtility.GetUnequalPairs(t.CreateIndexes));

            public static IEnumerable<object[]> ValidatorSpecialIndexTestData => ValidatorSpecialTestData(t => ValidatorTestUtility.GetSpecialPairs(t.CreateSpecialIndexes));

            [Theory]
            [MemberData(nameof(ValidatorEqualIndexTestData))]
            public async Task PassesIfEqual(
                RegistrationIndexValidator validator,
                PackageRegistrationIndexMetadata v2,
                PackageRegistrationIndexMetadata v3)
            {
                await validator.CompareIndexAsync(ValidatorTestUtility.GetFakeValidationContext(), v2, v3);
            }

            [Theory]
            [MemberData(nameof(ValidatorUnequalIndexTestData))]
            public async Task FailsIfUnequal(
                RegistrationIndexValidator validator,
                PackageRegistrationIndexMetadata v2,
                PackageRegistrationIndexMetadata v3)
            {
                await Assert.ThrowsAnyAsync<MetadataInconsistencyException>(
                    () => validator.CompareIndexAsync(ValidatorTestUtility.GetFakeValidationContext(), v2, v3));
            }

            [Theory]
            [MemberData(nameof(ValidatorSpecialIndexTestData))]
            public async Task SpecialCasesReturnAsExpected(
                RegistrationIndexValidator validator,
                PackageRegistrationIndexMetadata v2,
                PackageRegistrationIndexMetadata v3,
                bool shouldPass)
            {
                var compareTask = Task.Run(async () => await validator.CompareIndexAsync(ValidatorTestUtility.GetFakeValidationContext(), v2, v3));

                if (shouldPass)
                {
                    await compareTask;
                }
                else
                {
                    await Assert.ThrowsAnyAsync<MetadataInconsistencyException>(
                        () => compareTask);
                }
            }
        }

        public class TheShouldRunIndexAsyncMethod
        {
            public static IEnumerable<object[]> ValidatorRunIndexTestData => ValidatorTestData(t => ValidatorTestUtility.GetPairs(t.CreateIndexes));

            public static IEnumerable<object[]> ValidatorSkipIndexTestData => ValidatorTestData(t => ValidatorTestUtility.GetBigraphPairs(t.CreateIndexes, t.CreateSkippedIndexes));

            [Theory]
            [MemberData(nameof(ValidatorRunIndexTestData))]
            public async Task Runs(
                RegistrationIndexValidator validator,
                PackageRegistrationIndexMetadata v2,
                PackageRegistrationIndexMetadata v3)
            {
                Assert.True(await validator.ShouldRunIndexAsync(ValidatorTestUtility.GetFakeValidationContext(), v2, v3));
            }

            [Theory]
            [MemberData(nameof(ValidatorSkipIndexTestData))]
            public async Task Skips(
                RegistrationIndexValidator validator,
                PackageRegistrationIndexMetadata v2,
                PackageRegistrationIndexMetadata v3)
            {
                Assert.False(await validator.ShouldRunIndexAsync(ValidatorTestUtility.GetFakeValidationContext(), v2, v3));
            }
        }
    }
}