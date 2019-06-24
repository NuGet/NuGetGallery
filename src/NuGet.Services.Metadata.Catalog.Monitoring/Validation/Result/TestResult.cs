// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

namespace NuGet.Services.Metadata.Catalog.Monitoring
{
    /// <summary>
    /// The possible outcomes of an <see cref="IValidator"/>.
    /// </summary>
    public enum TestResult
    {
        /// <summary>
        /// The test completed successfully.
        /// </summary>
        Pass,

        /// <summary>
        /// The test failed and should be investigated.
        /// </summary>
        Fail,

        /// <summary>
        /// The test was skipped (<see cref="Validator.ShouldRunAsync(ValidationContext)"/> returned <see cref="ShouldRunTestResult.No"/>).
        /// </summary>
        Skip,

        /// <summary>
        /// The result of the test could not be determined (<see cref="Validator.ShouldRunAsync(ValidationContext)"/> returned <see cref="ShouldRunTestResult.RetryLater"/>).
        /// </summary>
        /// <remarks>
        /// This result is returned if there is a newer catalog entry for this package. When that catalog entry is processed, a different result should be returned.
        /// </remarks>
        Pending,
    }
}