// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using Newtonsoft.Json;

namespace NuGet.Services.Validation.Issues
{
    public abstract class ValidationIssue : IValidationIssue
    {
        /// <summary>
        /// The map of issue codes to the type that represents the issues. The types MUST extend <see cref="ValidationIssue"/>.
        /// </summary>
        public static readonly IReadOnlyDictionary<ValidationIssueCode, Type> IssueCodeTypes = new Dictionary<ValidationIssueCode, Type>
        {
            { ValidationIssueCode.PackageIsSigned, GetIssueType<PackageIsSigned>() },
            { ValidationIssueCode.SignedPackageMustHaveOneSignature, GetIssueType<SignedPackageMustHaveOneSignature>() },
            { ValidationIssueCode.ClientSigningVerificationFailure, GetIssueType<ClientSigningVerificationFailure>() },
#pragma warning disable 618
            { ValidationIssueCode.ObsoleteTesting, GetIssueType<ObsoleteTestingIssue>() }
#pragma warning restore 618
        };

        /// <summary>
        /// Deserialize an error code and data string into a <see cref="ValidationIssue"/>.
        /// </summary>
        /// <param name="errorCode">The error code that the error represents.</param>
        /// <param name="data">The error's serialized data, as serialized by <see cref="Serialize"/>.</param>
        /// <returns>An error object that can be used to display an error message to users.</returns>
        public static ValidationIssue Deserialize(ValidationIssueCode errorCode, string data)
        {
            if (!IssueCodeTypes.TryGetValue(errorCode, out Type deserializationType))
            {
                return new UnknownIssue();
            }

            try
            {
                var issue = JsonConvert.DeserializeObject(data, deserializationType) as ValidationIssue;

                /// <see cref="JsonConvert.DeserializeObject(string, Type)"/> can return null in some cases (for
                /// example if the input string is empty).
                return issue ?? new UnknownIssue();
            }
            catch (Exception)
            {
                return new UnknownIssue();
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
