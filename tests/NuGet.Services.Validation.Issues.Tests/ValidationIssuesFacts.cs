// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.IO;
using System.Linq;
using Newtonsoft.Json;
using Xunit;

namespace NuGet.Services.Validation.Issues.Tests
{
    public class ValidationIssuesFacts
    {
        [Fact]
        public void TheIssueCodeTypesPropertyValuesAllExtendValidationIssue()
        {
            Assert.True(ValidationIssue.IssueCodeTypes.Values.All(t => t.IsSubclassOf(typeof(ValidationIssue))));
        }

        public class TheSerializeMethod
        {
            [Fact]
            public void UnknownSerialization()
            {
                // Arrange
                var unknownIssue = new UnknownIssue();
                var result = unknownIssue.Serialize();

                // Assert
                Assert.Equal(Strings.EmptyJson, result);
            }

            [Fact]
            public void ObsoleteTestingIssueSerialization()
            {
                // Arrange
#pragma warning disable 618
                var signedError = new ObsoleteTestingIssue("Hello", 123);
#pragma warning restore 618
                var result = signedError.Serialize();

                // Assert
                Assert.Equal(Strings.ObsoleteTestingIssueJson, result);
            }

            [Fact]
            public void PackageIsSignedSerialization()
            {
                // Arrange
                var signedError = new PackageIsSigned();
                var result = signedError.Serialize();

                // Assert
                Assert.Equal(Strings.EmptyJson, result);
            }
        }

        public class TheDeserializeMethod
        {
            [Fact]
            public void UnknownDeserialization()
            {
                // Arrange & Act
                var validationIssue = CreatePackageValidationIssue(ValidationIssueCode.Unknown, Strings.EmptyJson);
                var result = ValidationIssue.Deserialize(validationIssue.IssueCode, validationIssue.Data) as UnknownIssue;

                // Assert
                Assert.NotNull(result);
                Assert.Equal(ValidationIssueCode.Unknown, result.IssueCode);
                Assert.Equal("Package validation failed due to an unknown error.", result.GetMessage());
            }

            [Fact]
            public void ObsoleteTestingIssueDeserialization()
            {
                // Arrange & Act
#pragma warning disable 618
                var validationIssue = CreatePackageValidationIssue(ValidationIssueCode.ObsoleteTesting, Strings.ObsoleteTestingIssueJson);
                var result = ValidationIssue.Deserialize(validationIssue.IssueCode, validationIssue.Data) as ObsoleteTestingIssue;
#pragma warning restore 618

                // Assert
                Assert.NotNull(result);
#pragma warning disable 618
                Assert.Equal(ValidationIssueCode.ObsoleteTesting, result.IssueCode);
#pragma warning restore 618
                Assert.Equal("Hello", result.A);
                Assert.Equal(123, result.B);
            }

            [Fact]
            public void InvalidDeserialization()
            {
                // Arrange & Act & Assert
                var validationIssue = CreatePackageValidationIssue(ValidationIssueCode.PackageIsSigned, Strings.InvalidJson);

                Assert.Throws<JsonReaderException>(() => ValidationIssue.Deserialize(validationIssue.IssueCode, validationIssue.Data));
            }

            private PackageValidationIssue CreatePackageValidationIssue(ValidationIssueCode issueCode, string data)
            {
                return new PackageValidationIssue
                {
                    IssueCode = issueCode,
                    Data = data
                };
            }
        }
    }
}
