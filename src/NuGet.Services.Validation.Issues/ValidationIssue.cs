// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using Newtonsoft.Json;

namespace NuGet.Services.Validation.Issues
{
    public abstract class ValidationIssue : IValidationIssue
    {
        public static ValidationIssue Unknown { get; } = new NoDataValidationIssue(ValidationIssueCode.Unknown);
        public static ValidationIssue PackageIsSigned { get; } = new NoDataValidationIssue(ValidationIssueCode.PackageIsSigned);
        public static ValidationIssue PackageIsZip64 { get; } = new NoDataValidationIssue(ValidationIssueCode.PackageIsZip64);
        public static ValidationIssue OnlyAuthorSignaturesSupported { get; } = new NoDataValidationIssue(ValidationIssueCode.OnlyAuthorSignaturesSupported);
        public static ValidationIssue AuthorAndRepositoryCounterSignaturesNotSupported { get; } = new NoDataValidationIssue(ValidationIssueCode.AuthorAndRepositoryCounterSignaturesNotSupported);
        public static ValidationIssue OnlySignatureFormatVersion1Supported { get; } = new NoDataValidationIssue(ValidationIssueCode.OnlySignatureFormatVersion1Supported);
        public static ValidationIssue AuthorCounterSignaturesNotSupported { get; } = new NoDataValidationIssue(ValidationIssueCode.AuthorCounterSignaturesNotSupported);
        public static ValidationIssue PackageIsNotSigned { get; } = new NoDataValidationIssue(ValidationIssueCode.PackageIsNotSigned);
        public static ValidationIssue SymbolErrorCode_ChecksumDoesNotMatch { get; } = new NoDataValidationIssue(ValidationIssueCode.SymbolErrorCode_ChecksumDoesNotMatch);
        public static ValidationIssue SymbolErrorCode_MatchingAssemblyNotFound { get; } = new NoDataValidationIssue(ValidationIssueCode.SymbolErrorCode_MatchingAssemblyNotFound);
        public static ValidationIssue SymbolErrorCode_PdbIsNotPortable { get; } = new NoDataValidationIssue(ValidationIssueCode.SymbolErrorCode_PdbIsNotPortable);
        public static ValidationIssue SymbolErrorCode_SnupkgDoesNotContainSymbols { get; } = new NoDataValidationIssue(ValidationIssueCode.SymbolErrorCode_SnupkgDoesNotContainSymbols);
        public static ValidationIssue SymbolErrorCode_SnupkgContainsEntriesNotSafeForExtraction { get; } = new NoDataValidationIssue(ValidationIssueCode.SymbolErrorCode_SnupkgContainsEntriesNotSafeForExtraction);

        /// <summary>
        /// The map of issue codes to the type that represents the issues. The types MUST extend <see cref="ValidationIssue"/>.
        /// </summary>
        internal static readonly IReadOnlyDictionary<ValidationIssueCode, Type> IssueCodeTypes = new Dictionary<ValidationIssueCode, Type>
        {
            { ValidationIssueCode.ClientSigningVerificationFailure, GetIssueType<ClientSigningVerificationFailure>() },
            { ValidationIssueCode.PackageIsSignedWithUnauthorizedCertificate, GetIssueType<UnauthorizedCertificateFailure>() },
#pragma warning disable 618
            { ValidationIssueCode.ObsoleteTesting, GetIssueType<ObsoleteTestingIssue>() }
#pragma warning restore 618
        };

        /// <summary>
        /// The set of issue codes that don't need a custom issue type. All of these codes use
        /// <see cref="NoDataValidationIssue"/> as their concrete type.
        /// </summary>
        internal static readonly ISet<ValidationIssueCode> IssueCodesWithNoData = new HashSet<ValidationIssueCode>
        {
            ValidationIssueCode.PackageIsSigned,
            ValidationIssueCode.PackageIsZip64,
            ValidationIssueCode.OnlyAuthorSignaturesSupported,
            ValidationIssueCode.AuthorAndRepositoryCounterSignaturesNotSupported,
            ValidationIssueCode.OnlySignatureFormatVersion1Supported,
            ValidationIssueCode.AuthorCounterSignaturesNotSupported,
            ValidationIssueCode.PackageIsNotSigned,
            ValidationIssueCode.SymbolErrorCode_ChecksumDoesNotMatch,
            ValidationIssueCode.SymbolErrorCode_MatchingAssemblyNotFound,
            ValidationIssueCode.SymbolErrorCode_PdbIsNotPortable,
            ValidationIssueCode.SymbolErrorCode_SnupkgDoesNotContainSymbols,
            ValidationIssueCode.SymbolErrorCode_SnupkgContainsEntriesNotSafeForExtraction,
        };

        /// <summary>
        /// Deserialize an error code and data string into a <see cref="ValidationIssue"/>.
        /// </summary>
        /// <param name="errorCode">The error code that the error represents.</param>
        /// <param name="data">The error's serialized data, as serialized by <see cref="Serialize"/>.</param>
        /// <returns>An error object that can be used to display an error message to users.</returns>
        public static ValidationIssue Deserialize(ValidationIssueCode errorCode, string data)
        {
            if (IssueCodesWithNoData.Contains(errorCode))
            {
                return new NoDataValidationIssue(errorCode);
            }

            if (!IssueCodeTypes.TryGetValue(errorCode, out Type deserializationType))
            {
                return Unknown;
            }

            try
            {
                var issue = JsonConvert.DeserializeObject(data, deserializationType) as ValidationIssue;

                /// <see cref="JsonConvert.DeserializeObject(string, Type)"/> can return null in some cases (for
                /// example if the input string is empty).
                return issue ?? Unknown;
            }
            catch (Exception)
            {
                return Unknown;
            }
        }

        /// <summary>
        /// Get the <see cref="Type"/> of a <see cref="ValidationIssue"/>. Used to populate <see cref="IssueCodeTypes"/>.
        /// </summary>
        /// <typeparam name="T">The compile-time type whose runtime type should be fetched.</typeparam>
        /// <returns>The error's runtime type.</returns>
        private static Type GetIssueType<T>() where T : ValidationIssue => typeof(T);

        /// <summary>
        /// The code that this issue represents.
        /// </summary>
        [JsonIgnore]
        public abstract ValidationIssueCode IssueCode { get; }

        /// <summary>
        /// Serialize this issue into a string, excluding the issue code.
        /// </summary>
        /// <returns>A serialized version of this validation issue, excluding the issue code.</returns>
        public virtual string Serialize() => JsonConvert.SerializeObject(this);
    }
}
