// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NuGet.Services.Validation;
using NuGet.Services.Validation.Issues;
using Xunit;

namespace NuGetGallery.Views.Packages
{
    public class ValidationIssueFacts
    {
        [Theory]
        [MemberData(nameof(HasACaseForAllIssueTypesTestData))]
        public void HasACaseForAllIssueTypes(ValidationIssue issue)
        {
            // Arrange & Act
            var template = GetTemplate();

            // Assert
            Assert.Contains("case ValidationIssueCode." + issue.IssueCode.ToString(), template);
        }

        [Theory]
        [MemberData(nameof(HasExpectedMessageForUnknownIssueTestData))]
        public void HasExpectedMessageForUnknownIssue(ValidationIssue issue)
        {
            // Arrange & Act
            var template = GetTemplate();

            // Assert
            Assert.DoesNotContain(issue.IssueCode.ToString(), template);
        }

        [Theory]
        [MemberData(nameof(AllIssueCodesAreHandledTestData))]
        public void AllIssueCodesAreHandled(ValidationIssueCode issueCode)
        {
            var issueCodes = new HashSet<ValidationIssueCode>(KnownValidationIssues
                .Select(x => x.IssueCode)
                .Concat(UnknownValidationIssues
                    .Select(x => x.IssueCode)));

            Assert.Contains(issueCode, issueCodes);
        }

        private string GetTemplate()
        {
            using (var stream = GetType().Assembly.GetManifestResourceStream("NuGetGallery.Views.Packages._ValidationIssue.cshtml"))
            using (var streamReader = new StreamReader(stream))
            {
                return streamReader.ReadToEnd();
            }
        }

        public static IEnumerable<ValidationIssue> KnownValidationIssues
        {
            get
            {
                yield return ValidationIssue.PackageIsSigned;
                yield return new ClientSigningVerificationFailure("NU3000", "Some signing error.");
                yield return ValidationIssue.PackageIsZip64;
                yield return ValidationIssue.OnlyAuthorSignaturesSupported;
                yield return ValidationIssue.AuthorAndRepositoryCounterSignaturesNotSupported;
                yield return ValidationIssue.OnlySignatureFormatVersion1Supported;
                yield return ValidationIssue.AuthorCounterSignaturesNotSupported;
                yield return ValidationIssue.PackageIsNotSigned;
                yield return ValidationIssue.SymbolErrorCode_ChecksumDoesNotMatch;
                yield return ValidationIssue.SymbolErrorCode_MatchingAssemblyNotFound;
                yield return ValidationIssue.SymbolErrorCode_PdbIsNotPortable;
                yield return ValidationIssue.SymbolErrorCode_SnupkgDoesNotContainSymbols;
                yield return ValidationIssue.SymbolErrorCode_SnupkgContainsEntriesNotSafeForExtraction;
#pragma warning disable 618
                yield return new UnauthorizedCertificateFailure("thumbprint");
#pragma warning restore 618
                yield return new UnauthorizedCertificateSha256Failure("thumbprint-sha1", "thumbprint-sha256");
                yield return new UnauthorizedAzureTrustedSigningCertificateFailure("thumbprint-sha1", "thumbprint-sha256", "1.2.3.4.5.6");
            }
        }

        public static IEnumerable<ValidationIssue> UnknownValidationIssues
        {
            get
            {
                yield return ValidationIssue.Unknown;
#pragma warning disable 0618
                yield return new ObsoleteTestingIssue("a", 1);
#pragma warning restore 0618
            }
        }

        public static IEnumerable<object[]> AllIssueCodesAreHandledTestData => Enum
            .GetValues(typeof(ValidationIssueCode))
            .Cast<ValidationIssueCode>()
            .Select(x => new object[] { x });

        public static IEnumerable<object[]> HasACaseForAllIssueTypesTestData => KnownValidationIssues
            .Select(x => new object[] { x });

        public static IEnumerable<object[]> HasExpectedMessageForUnknownIssueTestData => UnknownValidationIssues
            .Select(x => new object[] { x });
    }
}
