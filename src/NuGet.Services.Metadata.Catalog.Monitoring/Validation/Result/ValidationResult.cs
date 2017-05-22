// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;

namespace NuGet.Services.Metadata.Catalog.Monitoring
{
    /// <summary>
    /// Stores information about the outcome of a <see cref="IValidator"/>.
    /// </summary>
    public class ValidationResult
    {
        /// <summary>
        /// The test that was run.
        /// </summary>
        public IValidator Validator { get; set; }
        /// <summary>
        /// The result of the test.
        /// </summary>
        public TestResult Result { get; set; }
        /// <summary>
        /// If the test <see cref="TestResult.Fail"/>ed, the exception that was thrown.
        /// </summary>
        public Exception Exception { get; set; }
    }
}
