// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using NuGet.Jobs.Validation.Storage;
using NuGet.Services.Validation;
using Xunit;

namespace Validation.PackageSigning.Core.Tests.Storage
{
    public class ValidatorStatusExtensionsFacts
    {
        public class ToNuGetValidationResponse
        {
            [Fact]
            public void RejectsNullValidatorStatus()
            {
                // Arrange
                ValidatorStatus validatorStatus = null;

                // Act & Assert
                var ex = Assert.Throws<ArgumentNullException>(() => validatorStatus.ToNuGetValidationResponse());
                Assert.Equal("validatorStatus", ex.ParamName);
            }

            [Fact]
            public void RejectsNullValidatorIssues()
            {
                // Arrange
                var validatorStatus = new ValidatorStatus
                {
                    ValidatorIssues = null,
                };

                // Act & Assert
                var ex = Assert.Throws<ArgumentException>(() => validatorStatus.ToNuGetValidationResponse());
                Assert.Contains("The ValidatorIssues property must not be null.", ex.Message);
                Assert.Equal("validatorStatus", ex.ParamName);
            }

            [Fact]
            public void AllowsEmptyIssueList()
            {
                // Arrange
                var validatorStatus = new ValidatorStatus
                {
                    State = ValidationStatus.Succeeded,
                    ValidatorIssues = new List<ValidatorIssue>(),
                };

                // Act
                var result = validatorStatus.ToNuGetValidationResponse();

                // Assert
                Assert.Equal(ValidationStatus.Succeeded, result.Status);
                Assert.NotNull(result.Issues);
                Assert.Empty(result.Issues);
            }

            [Fact]
            public void IncludesNupkgUrlIfPresent()
            {
                // Arrange
                var nupkgUrl = "http://example/packages/nuget.versioning.4.6.0.nupkg";
                var validatorStatus = new ValidatorStatus
                {
                    State = ValidationStatus.Succeeded,
                    ValidatorIssues = new List<ValidatorIssue>(),
                    NupkgUrl = nupkgUrl,
                };

                // Act
                var result = validatorStatus.ToNuGetValidationResponse();

                // Assert
                Assert.Equal(ValidationStatus.Succeeded, result.Status);
                Assert.NotNull(result.Issues);
                Assert.Empty(result.Issues);
                Assert.Equal(nupkgUrl, result.NupkgUrl);
            }

            [Fact]
            public void BlindlyConvertsIssues()
            {
                // Arrange
                var validatorStatus = new ValidatorStatus
                {
                    State = ValidationStatus.Failed,
                    ValidatorIssues = new List<ValidatorIssue>
                    {
                        new ValidatorIssue { IssueCode = (ValidationIssueCode)int.MaxValue, Data = "unknown issue data" },
                        new ValidatorIssue { IssueCode = ValidationIssueCode.Unknown, Data = "{}" },
                        new ValidatorIssue { IssueCode = ValidationIssueCode.ClientSigningVerificationFailure, Data = "{\"invalid\":\"data\"}" },
                    },
                };

                // Act
                var result = validatorStatus.ToNuGetValidationResponse();

                // Assert
                Assert.Equal(ValidationStatus.Failed, result.Status);
                Assert.Equal(3, result.Issues.Count);

                Assert.Equal((ValidationIssueCode)int.MaxValue, result.Issues[0].IssueCode);
                Assert.Equal("unknown issue data", result.Issues[0].Serialize());

                Assert.Equal(ValidationIssueCode.Unknown, result.Issues[1].IssueCode);
                Assert.Equal("{}", result.Issues[1].Serialize());

                Assert.Equal(ValidationIssueCode.ClientSigningVerificationFailure, result.Issues[2].IssueCode);
                Assert.Equal("{\"invalid\":\"data\"}", result.Issues[2].Serialize());
            }
        }
    }
}
