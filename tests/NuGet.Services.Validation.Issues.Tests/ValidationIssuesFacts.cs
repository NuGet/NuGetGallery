// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
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

        [Theory]
        [MemberData(nameof(ConvenienceStaticFieldsHaveTheProperCodesData))]
        public void ConvenienceStaticFieldsHaveTheProperCodes(ValidationIssueCode code, IValidationIssue issue)
        {
            Assert.Equal(code, issue.IssueCode);
            Assert.IsType<NoDataValidationIssue>(issue);
        }

        public static IEnumerable<object[]> ConvenienceStaticFieldsHaveTheProperCodesData => IssuesWithNoProperties
            .Select(x => new object[] { x.Key, x.Value() });

        public class TheSerializeMethod
        {
            [Theory]
            [MemberData(nameof(SerializationOfIssuesWithNoPropertiesData))]
            public void SerializationOfIssuesWithNoProperties(ValidationIssueCode code)
            {
                // Arrange
                var issue = new NoDataValidationIssue(code);

                // Act
                var result = issue.Serialize();

                // Assert
                Assert.Equal(Strings.EmptyJson, result);
            }

            public static IEnumerable<object[]> SerializationOfIssuesWithNoPropertiesData => IssuesWithNoProperties
                .Select(x => new object[] { x.Key });

            [Fact]
            public void ObsoleteTestingIssueSerialization()
            {
                // Arrange
#pragma warning disable 618
                var issue = new ObsoleteTestingIssue("Hello", 123);
#pragma warning restore 618

                // Act
                var result = issue.Serialize();

                // Assert
                Assert.Equal(Strings.ObsoleteTestingIssueJson, result);
            }

            [Fact]
            public void ClientSigningVerificationFailureSerialization()
            {
                // Arrange
                var signedError = new ClientSigningVerificationFailure("NU3008", "The package integrity check failed.");

                // Act
                var result = signedError.Serialize();

                // Assert
                Assert.Equal(Strings.ClientSigningVerificationFailureIssueJson, result);
            }

            [Fact]
            public void UnauthorizedCertificateFailureSerialization()
            {
                // Arrange
                var signedError = new UnauthorizedCertificateFailure("thumbprint");

                // Act
                var result = signedError.Serialize();

                // Assert
                Assert.Equal(Strings.UnauthorizedCertificateFailureIssueJson, result);
            }
        }

        public class TheDeserializeMethod
        {
            [Theory]
            [MemberData(nameof(DeserializationOfIssuesWithNoPropertiesData))]
            public void DeserializationOfIssuesWithNoProperties(ValidationIssueCode code)
            {
                // Arrange
                var validationIssue = CreatePackageValidationIssue(code, Strings.EmptyJson);

                // Act
                var result = ValidationIssue.Deserialize(validationIssue.IssueCode, validationIssue.Data);

                // Assert
                Assert.NotNull(result);
                var issue = Assert.IsType<NoDataValidationIssue>(result);
                Assert.Equal(code, result.IssueCode);
            }

            public static IEnumerable<object[]> DeserializationOfIssuesWithNoPropertiesData
            {
                get
                {
                    foreach (var code in IssuesWithNoProperties.Keys)
                    {
                        yield return new object[] { code };
                    }
                }
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

            private static readonly IReadOnlyList<ValidationIssueCode> CodeWithProperties = Enum
                .GetValues(typeof(ValidationIssueCode))
                .Cast<ValidationIssueCode>()
                .Except(IssuesWithNoProperties.Keys)
                .ToList();
            
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
                "{}",
                "{\"foo\":\"bar\"}", // "foo" is never a valid property name so is therefore ignored.
            };

            public static IEnumerable<object[]> InvalidDeserializationData
            {
                get
                {
                    foreach (var code in CodeWithProperties)
                    {
                        foreach (var data in InvalidData)
                        {
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
            public void UnauthorizedCertificateFailureDeserialization()
            {
                // Arrange
                var validationIssue = CreatePackageValidationIssue(ValidationIssueCode.PackageIsSignedWithUnauthorizedCertificate, Strings.UnauthorizedCertificateFailureIssueJson);

                // Act
                var result = ValidationIssue.Deserialize(validationIssue.IssueCode, validationIssue.Data) as UnauthorizedCertificateFailure;

                // Assert
                Assert.NotNull(result);
                Assert.Equal(ValidationIssueCode.PackageIsSignedWithUnauthorizedCertificate, result.IssueCode);
                Assert.Equal("thumbprint", result.Sha1Thumbprint);
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

        public static readonly IDictionary<ValidationIssueCode, Func<IValidationIssue>> IssuesWithNoProperties = new Dictionary<ValidationIssueCode, Func<IValidationIssue>>
        {
            { ValidationIssueCode.Unknown, () => ValidationIssue.Unknown },
            { ValidationIssueCode.PackageIsSigned, () => ValidationIssue.PackageIsSigned },
            { ValidationIssueCode.PackageIsZip64, () => ValidationIssue.PackageIsZip64 },
            { ValidationIssueCode.OnlyAuthorSignaturesSupported, () => ValidationIssue.OnlyAuthorSignaturesSupported },
            { ValidationIssueCode.AuthorAndRepositoryCounterSignaturesNotSupported, () => ValidationIssue.AuthorAndRepositoryCounterSignaturesNotSupported },
            { ValidationIssueCode.OnlySignatureFormatVersion1Supported, () => ValidationIssue.OnlySignatureFormatVersion1Supported },
            { ValidationIssueCode.AuthorCounterSignaturesNotSupported, () => ValidationIssue.AuthorCounterSignaturesNotSupported },
            { ValidationIssueCode.PackageIsNotSigned, () => ValidationIssue.PackageIsNotSigned },
            { ValidationIssueCode.SymbolErrorCode_ChecksumDoesNotMatch, () => ValidationIssue.SymbolErrorCode_ChecksumDoesNotMatch },
            { ValidationIssueCode.SymbolErrorCode_MatchingAssemblyNotFound, () => ValidationIssue.SymbolErrorCode_MatchingAssemblyNotFound },
            { ValidationIssueCode.SymbolErrorCode_PdbIsNotPortable, () => ValidationIssue.SymbolErrorCode_PdbIsNotPortable },
            { ValidationIssueCode.SymbolErrorCode_SnupkgDoesNotContainSymbols, () => ValidationIssue.SymbolErrorCode_SnupkgDoesNotContainSymbols },
        };
    }
}
