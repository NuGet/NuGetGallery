// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using Xunit;

namespace NuGet.Services.Validation
{
    public class ValidationErrorCodeFacts
    {
        private static readonly Dictionary<int, ValidationIssueCode> Expected = new Dictionary<int, ValidationIssueCode>
        {
            { 0, ValidationIssueCode.Unknown },
            { 1, ValidationIssueCode.PackageIsSigned },
            { 2, ValidationIssueCode.ClientSigningVerificationFailure },
            { 3, ValidationIssueCode.PackageIsZip64 },
            { 4, ValidationIssueCode.OnlyAuthorSignaturesSupported },
            { 5, ValidationIssueCode.AuthorAndRepositoryCounterSignaturesNotSupported },
            { 6, ValidationIssueCode.OnlySignatureFormatVersion1Supported },
            { 7, ValidationIssueCode.AuthorCounterSignaturesNotSupported },
            { 8, ValidationIssueCode.PackageIsNotSigned },
            { 9, ValidationIssueCode.PackageIsSignedWithUnauthorizedCertificate },
            { 250, ValidationIssueCode.SymbolErrorCode_ChecksumDoesNotMatch },
            { 251, ValidationIssueCode.SymbolErrorCode_MatchingAssemblyNotFound},
            { 252, ValidationIssueCode.SymbolErrorCode_PdbIsNotPortable},
            { 253, ValidationIssueCode.SymbolErrorCode_SnupkgDoesNotContainSymbols},
#pragma warning disable 618
            { 9999, ValidationIssueCode.ObsoleteTesting },
#pragma warning restore 618
        };

        public static IEnumerable<object[]> HasUnchangingValuesData => Expected
            .Select(x => new object[] { x.Key, x.Value });

        /// <summary>
        /// This enum is persisted so the integer values must not change.
        /// </summary>
        [Theory]
        [MemberData(nameof(HasUnchangingValuesData))]
        public void HasUnchangingValues(int expected, ValidationStatus input)
        {
            Assert.Equal(expected, (int)input);
        }

        [Fact]
        public void HasAllValuesTest()
        {
            Assert.Equal(
                Expected.Select(x => x.Value).OrderBy(x => x),
                Enum.GetValues(typeof(ValidationIssueCode)).Cast<ValidationIssueCode>().OrderBy(x => x));
        }
    }
}
