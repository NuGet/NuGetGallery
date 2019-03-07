// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;

namespace NuGet.Services.Validation
{
    /// <summary>
    /// The codes for <see cref="IValidationIssue"/>.
    /// </summary>
    public enum ValidationIssueCode
    {
        /// <summary>
        /// An unknown issue has occurred.
        /// </summary>
        Unknown = 0,

        /// <summary>
        /// Signed packages are blocked.
        /// </summary>
        PackageIsSigned = 1,

        /// <summary>
        /// A signing verification failure occurred, where the issue message is mastered by the client APIs.
        /// </summary>
        ClientSigningVerificationFailure = 2,

        /// <summary>
        /// Zip64 packages are not allowed.
        /// </summary>
        PackageIsZip64 = 3,

        /// <summary>
        /// Packages pushed should only have author signatures.
        /// </summary>
        OnlyAuthorSignaturesSupported = 4,

        /// <summary>
        /// Counter signatures on a package signature signed CMS that have the author or repository commitment type are
        /// not supported.
        /// </summary>
        AuthorAndRepositoryCounterSignaturesNotSupported = 5,

        /// <summary>
        /// Only package signature version 1 is supported.
        /// </summary>
        OnlySignatureFormatVersion1Supported = 6,

        /// <summary>
        /// Counter signatures on a package signature signed CMS that have the author commitment type are not
        /// supported.
        /// </summary>
        AuthorCounterSignaturesNotSupported = 7,

        /// <summary>
        /// A package that is configured to require signing is not signed.
        /// </summary>
        PackageIsNotSigned = 8,

        /// <summary>
        /// A package is signed with an unauthorized certificate.
        /// </summary>
        PackageIsSignedWithUnauthorizedCertificate = 9,

        #region SymbolErrorCodes - reserved 200 - 299 segment
        /// <summary>
        /// Symbol checksum does not match with the binary assembly.
        /// </summary>
        SymbolErrorCode_ChecksumDoesNotMatch = 250,

        /// <summary>
        /// The pdb does not have a binary assembly file associated.
        /// </summary>
        SymbolErrorCode_MatchingAssemblyNotFound = 251,

        /// <summary>
        /// The pdb is not portable.
        /// </summary>
        SymbolErrorCode_PdbIsNotPortable = 252,

        /// <summary>
        /// The snupkg does not contain pdbs.
        /// </summary>
        SymbolErrorCode_SnupkgDoesNotContainSymbols = 253,
        #endregion

        /// <summary>
        /// Obsolete testing issue - do NOT use this!
        /// </summary>
        [Obsolete("This issue code should only be used for testing")]
        ObsoleteTesting = 9999,
    }
}