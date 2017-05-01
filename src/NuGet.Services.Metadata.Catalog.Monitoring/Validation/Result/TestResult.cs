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
        /// The test was skipped because the <see cref="IValidator.ShouldRun(ValidationContext)"/> condition returned false.
        /// </summary>
        Skip
    };
}
