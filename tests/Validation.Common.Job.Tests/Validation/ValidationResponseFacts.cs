using System;
using System.Collections.Generic;
using Xunit;

namespace NuGet.Services.Validation
{
    public class ValidationResponseFacts
    {
        public class Constructor
        {
            private const string outputUrl = "http://example/nuget.versioning.4.5.0.nupkg";

            [Theory]
            [InlineData(ValidationStatus.NotStarted)]
            [InlineData(ValidationStatus.Incomplete)]
            public void RejectResultToEmptyListForNonTerminalState(ValidationStatus status)
            {
                var results = new List<PackageValidationResult> { null };

                var ex = Assert.Throws<ArgumentException>(() => new ValidationResponse(status, results));
                Assert.Equal("status", ex.ParamName);
                Assert.Contains("Cannot specify results if the validation is not in a terminal status.", ex.Message);
            }

            [Theory]
            [InlineData(ValidationStatus.NotStarted)]
            [InlineData(ValidationStatus.Incomplete)]
            [InlineData(ValidationStatus.Failed)]
            public void RejectsOutputUrlForNonSuccessStatuses(ValidationStatus status)
            {
                var ex = Assert.Throws<ArgumentException>(() => new ValidationResponse(status, outputUrl));
                Assert.Equal("status", ex.ParamName);
                Assert.Contains("Cannot specify outputUrl if the validation is not in a success state.", ex.Message);
            }

            [Fact]
            public void AllowsOutputUrlForSuccessStatus()
            {
                var target = new ValidationResponse(ValidationStatus.Succeeded, outputUrl);
                Assert.Same(target.OutputUrl, outputUrl);
            }
        }
    }
}
