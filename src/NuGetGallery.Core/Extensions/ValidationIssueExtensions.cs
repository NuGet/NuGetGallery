// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using NuGet.Services.Validation;
using NuGet.Services.Validation.Issues;

namespace NuGetGallery
{
    internal static class ValidationIssueExtensions
    {
        /// <summary>
        /// Returns a Markdown representation describing the <see cref="ValidationIssue"/> in a user-friendly way.
        /// </summary>
        public static string ToMarkdownString(this ValidationIssue validationIssue, string announcementsUrl, string twitterUrl)
        {
            if (validationIssue == null)
            {
                throw new ArgumentNullException(nameof(validationIssue));
            }

            if (announcementsUrl == null)
            {
                throw new ArgumentNullException(nameof(announcementsUrl));
            }

            if (twitterUrl == null)
            {
                throw new ArgumentNullException(nameof(twitterUrl));
            }

            switch (validationIssue.IssueCode)
            {
                case ValidationIssueCode.PackageIsSigned:
                    return $"This package could not be published since it is signed. We do not accept signed packages at this moment. To be notified about package signing and more, watch our [Announcements]({announcementsUrl}) page or follow us on [Twitter]({twitterUrl}).";
                case ValidationIssueCode.ClientSigningVerificationFailure:
                    var clientIssue = (ClientSigningVerificationFailure)validationIssue;
                    return clientIssue != null
                        ? $"**{clientIssue.ClientCode}**: {clientIssue.ClientMessage}"
                        : "This package's signature was unable to get verified.";
                case ValidationIssueCode.PackageIsZip64:
                    return "Zip64 packages are not supported.";
                case ValidationIssueCode.OnlyAuthorSignaturesSupported:
                    return "Signed packages must only have an author signature. Other signature types are not supported.";
                case ValidationIssueCode.AuthorAndRepositoryCounterSignaturesNotSupported:
                    return "Author countersignatures and repository countersignatures are not supported.";
                case ValidationIssueCode.OnlySignatureFormatVersion1Supported:
                    return "**NU3007:** Package signatures must have format version 1.";
                case ValidationIssueCode.AuthorCounterSignaturesNotSupported:
                    return "Author countersignatures are not supported.";
                case ValidationIssueCode.PackageIsNotSigned:
                    return "This package must be signed with a registered certificate. [Read more...](https://aka.ms/nuget-signed-ref)";
                case ValidationIssueCode.PackageIsSignedWithUnauthorizedCertificate:
                    var certIssue = (UnauthorizedCertificateFailure)validationIssue;
                    return $"The package was signed, but the signing certificate {(certIssue != null ? $"(SHA-1 thumbprint {certIssue.Sha1Thumbprint})" : "")} is not associated with your account. You must register this certificate to publish signed packages. [Read more...](https://aka.ms/nuget-signed-ref)";
                case ValidationIssueCode.SymbolErrorCode_ChecksumDoesNotMatch:
                    return "The checksum does not match for the dll(s) and corresponding pdb(s).";
                case ValidationIssueCode.SymbolErrorCode_MatchingAssemblyNotFound:
                    return "The uploaded symbols package contains pdb(s) for a corresponding dll(s) not found in the nuget package.";
                case ValidationIssueCode.SymbolErrorCode_PdbIsNotPortable:
                    return "The uploaded symbols package contains one or more pdbs that are not portable.";
                case ValidationIssueCode.SymbolErrorCode_SnupkgDoesNotContainSymbols:
                    return "The uploaded symbols package does not contain any symbol files.";
                case ValidationIssueCode.SymbolErrorCode_SnupkgContainsEntriesNotSafeForExtraction:
                    return "The uploaded symbols package contains entries that are not safe for extraction.";
                default:
                    return "There was an unknown failure when validating your package.";
            }
        }
    }
}