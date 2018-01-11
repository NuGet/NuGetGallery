// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
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

            [Fact]
            public void ClientSigningVerificationFailureSerialization()
            {
                // Arrange
                var signedError = new ClientSigningVerificationFailure("NU3008", "The package integrity check failed.");
                var result = signedError.Serialize();

                // Assert
                Assert.Equal(Strings.ClientSigningVerificationFailureIssueJson, result);
            }

            [Fact]
            public void SignedPackageMustHaveOneSignatureSerialization()
            {
                // Arrange
                var signedError = new SignedPackageMustHaveOneSignature(count: 2);
                var result = signedError.Serialize();

                // Assert
                Assert.Equal(Strings.SignedPackageMustHaveOneSignatureIssueJson, result);
            }
        }

        public class TheDeserializeMethod
        {
            [Fact]
            public void UnknownDeserialization()
            {
                // Arrange
                var validationIssue = CreatePackageValidationIssue(ValidationIssueCode.Unknown, Strings.EmptyJson);

                // Act
                var result = ValidationIssue.Deserialize(validationIssue.IssueCode, validationIssue.Data) as UnknownIssue;

                // Assert
                Assert.NotNull(result);
                Assert.Equal(ValidationIssueCode.Unknown, result.IssueCode);
            }

            [Theory]
            [MemberData(nameof(InvalidDeserializationData))]
            public void InvalidDataDeserialization(ValidationIssueCode code, string data)
            {
                // Arrange
                var validationIssue = CreatePackageValidationIssue(code, data);

                // Act
                var result = ValidationIssue.Deserialize(validationIssue.IssueCode, validationIssue.Data);

                // Assert
                Assert.NotNull(result);
                Assert.Equal(ValidationIssueCode.Unknown, result.IssueCode);
                Assert.Equal(Strings.EmptyJson, result.Serialize());
            }

            private static readonly IReadOnlyList<ValidationIssueCode> CodesWithNoProperties = new[]
            {
                ValidationIssueCode.PackageIsSigned,
                ValidationIssueCode.Unknown,
            };

            private static readonly IReadOnlyList<ValidationIssueCode> Codes = Enum
                .GetValues(typeof(ValidationIssueCode))
                .Cast<ValidationIssueCode>()
                .Except(new[] { ValidationIssueCode.Unknown })
                .ToList();

            private static readonly IReadOnlyList<string> DataWithNoProperties = new[]
            {
                "{}",
                "{\"foo\":\"bar\"}", // "foo" is never a valid property name so is therefore ignored.
            };

            private static readonly IReadOnlyList<string> InvalidData = new[]
            {
                null,
                "",
                " ",
                "   \r\n \t ",
                "Hello this is dog",
                "[]",
                "null",
                "1",
                "\"foo\"",
                "2.3",
            }.Concat(DataWithNoProperties).ToList();

            public static IEnumerable<object[]> InvalidDeserializationData
            {
                get
                {
                    foreach (var code in Codes)
                    {
                        foreach (var data in InvalidData)
                        {
                            // Data that represents a JSON object with no properties is valid for validation issues with
                            // no properties. Therefore, don't emit test data for these cases.
                            if (DataWithNoProperties.Contains(data)
                                && CodesWithNoProperties.Contains(code))
                            {
                                continue;
                            }

                            yield return new object[] { code, data };
                        }
                    }
                }
            }

            [Fact]
            public void ObsoleteTestingIssueDeserialization()
            {
                // Arrange
#pragma warning disable 618
                var validationIssue = CreatePackageValidationIssue(ValidationIssueCode.ObsoleteTesting, Strings.ObsoleteTestingIssueJson);

                // Act
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
            public void PackageIsSignedDeserialization()
            {
                // Arrange
                var validationIssue = CreatePackageValidationIssue(ValidationIssueCode.PackageIsSigned, Strings.EmptyJson);

                // Act
                var result = ValidationIssue.Deserialize(validationIssue.IssueCode, validationIssue.Data);

                // Assert
                Assert.NotNull(result);
                Assert.Equal(ValidationIssueCode.PackageIsSigned, result.IssueCode);
            }

            [Fact]
            public void ClientSigningVerificationFailureDeserialization()
            {
                // Arrange
                var validationIssue = CreatePackageValidationIssue(ValidationIssueCode.ClientSigningVerificationFailure, Strings.ClientSigningVerificationFailureIssueJson);

                // Act
                var result = ValidationIssue.Deserialize(validationIssue.IssueCode, validationIssue.Data) as ClientSigningVerificationFailure;

                // Assert
                Assert.NotNull(result);
                Assert.Equal(ValidationIssueCode.ClientSigningVerificationFailure, result.IssueCode);
                Assert.Equal("NU3008", result.ClientCode);
                Assert.Equal("The package integrity check failed.", result.ClientMessage);
            }

            [Fact]
            public void SignedPackageMustHaveOneSignatureDeserialization()
            {
                // Arrange
                var validationIssue = CreatePackageValidationIssue(ValidationIssueCode.SignedPackageMustHaveOneSignature, Strings.SignedPackageMustHaveOneSignatureIssueJson);

                // Act
                var result = ValidationIssue.Deserialize(validationIssue.IssueCode, validationIssue.Data) as SignedPackageMustHaveOneSignature;

                // Assert
                Assert.NotNull(result);
                Assert.Equal(ValidationIssueCode.SignedPackageMustHaveOneSignature, result.IssueCode);
                Assert.Equal(2, result.Count);
            }

            [Fact]
            public void SignedPackageMustHaveOneSignatureDeserializationWhenCountIsInvalid()
            {
                // Arrange
                var validationIssue = CreatePackageValidationIssue(ValidationIssueCode.SignedPackageMustHaveOneSignature, Strings.SignedPackageMustHaveOneSignatureIssueJsonInvalidCount);

                // Act
                var result = ValidationIssue.Deserialize(validationIssue.IssueCode, validationIssue.Data);

                // Assert
                Assert.IsType<UnknownIssue>(result);
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
