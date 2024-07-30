// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Threading.Tasks;

namespace NuGet.Services.Metadata.Catalog.Monitoring
{
    /// <summary>
    /// The possible outcomes of <see cref="Validator.ShouldRunAsync(ValidationContext)"/>.
    /// </summary>
    public enum ShouldRunTestResult
    {
        /// <summary>
        /// The test should run.
        /// </summary>
        Yes,

        /// <summary>
        /// The test can be safely skipped.
        /// </summary>
        No,

        /// <summary>
        /// The test must be attempted again when more information is available.
        /// </summary>
        /// <remarks>
        /// Typically, this suggests that there is a newer catalog entry for this package. When that catalog entry is processed, a different result should be returned.
        /// </remarks>
        RetryLater,
    }

    public static class ShouldRunTestUtility
    {
        public static async Task<ShouldRunTestResult> Combine(params Func<Task<ShouldRunTestResult>>[] getResults)
        {
            foreach (var getResult in getResults)
            {
                var result = await getResult();
                if (result != ShouldRunTestResult.Yes)
                {
                    return result;
                }
            }

            return ShouldRunTestResult.Yes;
        }
    }
}