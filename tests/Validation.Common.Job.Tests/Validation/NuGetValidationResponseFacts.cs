// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using Xunit;

namespace NuGet.Services.Validation
{
    public class NuGetValidationResponseFacts
    {
        public class Constructor
        {
            private const string NupkgUrl = "http://example/nuget.versioning.4.5.0.nupkg";

            [Fact]
            public void DefaultsNullIssuesToEmptyList()
            {
                var target = new NuGetValidationResponse(ValidationStatus.Succeeded, issues: null);

                Assert.NotNull(target.Issues);
                Assert.Empty(target.Issues);
            }

            [Theory]
            [InlineData(ValidationStatus.NotStarted)]
            [InlineData(ValidationStatus.Incomplete)]
            public void RejectsIssesOnNonTerminalStatus(ValidationStatus status)
            {
                var issues = new List<IValidationIssue> { null };

                var ex = Assert.Throws<ArgumentException>(() => new NuGetValidationResponse(status, issues));
                Assert.Equal("status", ex.ParamName);
                Assert.Contains("Cannot specify issues if the validation is not in a terminal status.", ex.Message);
            }

            [Theory]
            [InlineData(ValidationStatus.NotStarted)]
            [InlineData(ValidationStatus.Incomplete)]
            [InlineData(ValidationStatus.Failed)]
            public void RejectsNupkgUrlForNonSucessStatuses(ValidationStatus status)
            {
                var ex = Assert.Throws<ArgumentException>(() => new NuGetValidationResponse(status, NupkgUrl));
                Assert.Equal("status", ex.ParamName);
                Assert.Contains("The nupkgUrl can only be provided when the status is Succeeded.", ex.Message);
            }

            [Fact]
            public void AllowsNupkgUrlForSuccessStatus()
            {
                var target = new NuGetValidationResponse(ValidationStatus.Succeeded, NupkgUrl);
                Assert.Same(target.NupkgUrl, NupkgUrl);
            }
        }
    }
}