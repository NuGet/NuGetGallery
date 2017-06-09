// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Threading.Tasks;
using NuGet.Services.Metadata.Catalog.Monitoring;
using Xunit;

namespace NgTests
{
    public class ValidatorTests
    {
        [Fact]
        public async Task Run_ReturnsPass()
        {
            // Arrange
            Func<bool> shouldRun = () => true;
            Action runInternal = () => { };

            var validator = new TestableValidator(shouldRun, runInternal);

            var data = new ValidationContext();

            // Act
            var result = await validator.ValidateAsync(data);

            // Assert
            Assert.Same(validator, result.Validator);
            Assert.Equal(TestResult.Pass, result.Result);
            Assert.Null(result.Exception);
        }

        [Fact]
        public async Task Run_ReturnsSkip()
        {
            // Arrange
            Func<bool> shouldRun = () => false;
            Action runInternal = () => { };

            var validator = new TestableValidator(shouldRun, runInternal);

            var data = new ValidationContext();

            // Act
            var result = await validator.ValidateAsync(data);

            // Assert
            Assert.Same(validator, result.Validator);
            Assert.Equal(TestResult.Skip, result.Result);
            Assert.Null(result.Exception);
        }

        [Fact]
        public async Task Run_ReturnsFail()
        {
            // Arrange
            var exception = new Exception();

            Func<bool> shouldRun = () => true;
            Action runInternal = () => { throw exception; };

            var validator = new TestableValidator(shouldRun, runInternal);

            var data = new ValidationContext();

            // Act
            var result = await validator.ValidateAsync(data);

            // Assert
            Assert.Same(validator, result.Validator);
            Assert.Equal(TestResult.Fail, result.Result);
            Assert.Same(exception, result.Exception);
        }
    }

    public class TestableValidator : Validator
    {
        public TestableValidator(Func<bool> shouldRun, Action runInternal)
        {
            _shouldRun = shouldRun;
            _runInternal = runInternal;
        }

        protected override Task<bool> ShouldRun(ValidationContext data)
        {
            return Task.FromResult(_shouldRun());
        }

        protected override Task RunInternal(ValidationContext data)
        {
            _runInternal();

            return Task.FromResult(0);
        }

        private Func<bool> _shouldRun;
        private Action _runInternal;
    }
}
