// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using Newtonsoft.Json;

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
        [JsonProperty("validator")]
        public IValidatorIdentity Validator { get; }

        /// <summary>
        /// The result of the test.
        /// </summary>
        [JsonProperty("result")]
        public TestResult Result { get; }

        /// <summary>
        /// If the test <see cref="TestResult.Fail"/>ed, the exception that was thrown.
        /// </summary>
        [JsonProperty("exception")]
        [JsonConverter(typeof(SafeExceptionConverter))]
        public Exception Exception { get; }

        public ValidationResult(IValidator validator, TestResult result)
            : this(validator, result, null)
        {
        }

        public ValidationResult(IValidatorIdentity validator, TestResult result, Exception exception)
        {
            Validator = validator;
            Result = result;
            Exception = exception;
        }

        [JsonConstructor]
        public ValidationResult(ValidatorIdentity validator, TestResult result, Exception exception)
        {
            Validator = validator;
            Result = result;
            Exception = exception;
        }
    }
}
