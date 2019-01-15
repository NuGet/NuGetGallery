// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using NuGet.Services.Metadata.Catalog.Monitoring;
using Xunit;

namespace NgTests.Validation
{
    public class RegistrationLeafValidatorTests
    {
        private static IEnumerable<object[]> ValidatorTestData<T>(Func<IRegistrationLeafValidatorTestData, IEnumerable<Tuple<T, T>>> getPairs)
        {
            foreach (var testData in ValidatorTestUtility.GetImplementations<IRegistrationLeafValidatorTestData>())
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

        private static IEnumerable<object[]> ValidatorSpecialTestData<T>(Func<IRegistrationLeafValidatorTestData, IEnumerable<Tuple<T, T, bool>>> getPairs)
        {
            foreach (var testData in ValidatorTestUtility.GetImplementations<IRegistrationLeafValidatorTestData>())
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

        public class TheCompareLeafMethod
        {
            public class OnIndex
            {
                public static IEnumerable<object[]> ValidatorEqualIndexTestData => ValidatorTestData(t => ValidatorTestUtility.GetEqualPairs(t.CreateIndexes));

                public static IEnumerable<object[]> ValidatorUnequalIndexTestData => ValidatorTestData(t => ValidatorTestUtility.GetUnequalPairs(t.CreateIndexes));

                public static IEnumerable<object[]> ValidatorSpecialIndexTestData => ValidatorSpecialTestData(t => ValidatorTestUtility.GetSpecialPairs(t.CreateSpecialIndexes));

                [Theory]
                [MemberData(nameof(ValidatorEqualIndexTestData))]
                public async Task PassesIfEqual(
                    RegistrationLeafValidator validator,
                    PackageRegistrationIndexMetadata v2,
                    PackageRegistrationIndexMetadata v3)
                {
                    await validator.CompareLeafAsync(ValidatorTestUtility.GetFakeValidationContext(), v2, v3);
                }

                [Theory]
                [MemberData(nameof(ValidatorUnequalIndexTestData))]
                public async Task FailsIfUnequal(
                    RegistrationLeafValidator validator,
                    PackageRegistrationIndexMetadata v2,
                    PackageRegistrationIndexMetadata v3)
                {
                    await Assert.ThrowsAnyAsync<MetadataInconsistencyException>(
                        () => validator.CompareLeafAsync(ValidatorTestUtility.GetFakeValidationContext(), v2, v3));
                }

                [Theory]
                [MemberData(nameof(ValidatorSpecialIndexTestData))]
                public async Task SpecialCasesReturnAsExpected(
                    RegistrationLeafValidator validator,
                    PackageRegistrationIndexMetadata v2,
                    PackageRegistrationIndexMetadata v3,
                    bool shouldPass)
                {
                    var compareTask = Task.Run(async () => await validator.CompareLeafAsync(ValidatorTestUtility.GetFakeValidationContext(), v2, v3));

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

            public class OnLeaf
            {
                public static IEnumerable<object[]> ValidatorEqualLeafTestData => ValidatorTestData(t => ValidatorTestUtility.GetEqualPairs(t.CreateLeafs));

                public static IEnumerable<object[]> ValidatorUnequalLeafTestData => ValidatorTestData(t => ValidatorTestUtility.GetUnequalPairs(t.CreateLeafs));

                public static IEnumerable<object[]> ValidatorSpecialIndexTestData => ValidatorSpecialTestData(t => ValidatorTestUtility.GetSpecialPairs(t.CreateSpecialLeafs));

                [Theory]
                [MemberData(nameof(ValidatorEqualLeafTestData))]
                public async Task PassesIfEqual(
                    RegistrationLeafValidator validator,
                    PackageRegistrationLeafMetadata v2,
                    PackageRegistrationLeafMetadata v3)
                {
                    await validator.CompareLeafAsync(ValidatorTestUtility.GetFakeValidationContext(), v2, v3);
                }

                [Theory]
                [MemberData(nameof(ValidatorUnequalLeafTestData))]
                public async Task FailsIfUnequal(
                    RegistrationLeafValidator validator,
                    PackageRegistrationLeafMetadata v2,
                    PackageRegistrationLeafMetadata v3)
                {
                    await Assert.ThrowsAnyAsync<MetadataInconsistencyException>(
                        () => validator.CompareLeafAsync(ValidatorTestUtility.GetFakeValidationContext(), v2, v3));
                }

                [Theory]
                [MemberData(nameof(ValidatorSpecialIndexTestData))]
                public async Task SpecialCasesReturnAsExpected(
                    RegistrationLeafValidator validator,
                    PackageRegistrationLeafMetadata v2,
                    PackageRegistrationLeafMetadata v3,
                    bool shouldPass)
                {
                    var compareTask = Task.Run(async () => await validator.CompareLeafAsync(ValidatorTestUtility.GetFakeValidationContext(), v2, v3));

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
        }

        public class TheShouldRunLeafMethod
        {
            public class OnIndex
            {
                public static IEnumerable<object[]> ValidatorRunIndexTestData => ValidatorTestData(t => ValidatorTestUtility.GetPairs(t.CreateIndexes));

                public static IEnumerable<object[]> ValidatorSkipIndexTestData => ValidatorTestData(t => ValidatorTestUtility.GetBigraphPairs(t.CreateIndexes, t.CreateSkippedIndexes));

                [Theory]
                [MemberData(nameof(ValidatorRunIndexTestData))]
                public async Task Runs(
                    RegistrationLeafValidator validator,
                    PackageRegistrationIndexMetadata v2,
                    PackageRegistrationIndexMetadata v3)
                {
                    Assert.Equal(true, await validator.ShouldRunLeafAsync(ValidatorTestUtility.GetFakeValidationContext(), v2, v3));
                }

                [Theory]
                [MemberData(nameof(ValidatorSkipIndexTestData))]
                public async Task Skips(
                    RegistrationLeafValidator validator,
                    PackageRegistrationIndexMetadata v2,
                    PackageRegistrationIndexMetadata v3)
                {
                    Assert.Equal(false, await validator.ShouldRunLeafAsync(ValidatorTestUtility.GetFakeValidationContext(), v2, v3));
                }
            }

            public class OnLeaf
            {
                public static IEnumerable<object[]> ValidatorRunLeafTestData => ValidatorTestData(t => ValidatorTestUtility.GetPairs(t.CreateLeafs));

                public static IEnumerable<object[]> ValidatorSkipLeafTestData => ValidatorTestData(t => ValidatorTestUtility.GetBigraphPairs(t.CreateLeafs, t.CreateSkippedLeafs));

                [Theory]
                [MemberData(nameof(ValidatorRunLeafTestData))]
                public async Task Runs(
                    RegistrationLeafValidator validator,
                    PackageRegistrationLeafMetadata v2,
                    PackageRegistrationLeafMetadata v3)
                {
                    Assert.Equal(true, await validator.ShouldRunLeafAsync(ValidatorTestUtility.GetFakeValidationContext(), v2, v3));
                }

                [Theory]
                [MemberData(nameof(ValidatorSkipLeafTestData))]
                public async Task Skips(
                    RegistrationLeafValidator validator,
                    PackageRegistrationLeafMetadata v2,
                    PackageRegistrationLeafMetadata v3)
                {
                    Assert.Equal(false, await validator.ShouldRunLeafAsync(ValidatorTestUtility.GetFakeValidationContext(), v2, v3));
                }
            }
        }
    }
}