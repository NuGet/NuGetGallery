// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Threading.Tasks;
using NuGet.Services.Metadata.Catalog.Monitoring;

namespace NgTests
{
    public class DummyValidator : IValidator
    {
        private TestResult _result;
        private Exception _exception;

        public string Name => nameof(DummyValidator);

        public DummyValidator(TestResult result, Exception e)
        {
            _result = result;
            _exception = e;
        }

        public Task<ValidationResult> ValidateAsync(ValidationContext context)
        {
            return Task.FromResult(Validate());
        }

        public ValidationResult Validate()
        {
            return new ValidationResult(this, _result, _exception);
        }
    }
}