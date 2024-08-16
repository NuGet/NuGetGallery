// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Linq;
using NuGet.Services.Validation;

namespace NuGet.Jobs.Validation.Storage
{
    public static class ValidatorStatusExtensions
    {
        /// <summary>
        /// Maps the provided validation status entity and its associated issues to a <see cref="INuGetValidationResponse"/>.
        /// This method does not attempt to deserialize the issue data.
        /// </summary>
        public static INuGetValidationResponse ToNuGetValidationResponse(this ValidatorStatus validatorStatus)
        {
            if (validatorStatus == null)
            {
                throw new ArgumentNullException(nameof(validatorStatus));
            }

            if (validatorStatus.ValidatorIssues == null)
            {
                throw new ArgumentException(
                    $"The {nameof(ValidatorStatus.ValidatorIssues)} property must not be null.",
                    nameof(validatorStatus));
            }

            /// Don't attempt to deserialize the issues. Instead, pass the issue code and data along to the orchestrator
            /// to be persisted as-is. This makes the orchestrator more resilient when having outdated version of the
            /// issues library. Note that this essentially assumes that the data stored in <see cref="ValidatorIssue"/>
            /// and <see cref="PackageValidationIssue"/> have the same schema.
            var issues = validatorStatus
                .ValidatorIssues
                .Select(x => new SerializedValidationIssue(x.IssueCode, x.Data))
                .ToList();

            return new NuGetValidationResponse(validatorStatus.State, issues, validatorStatus.NupkgUrl);
        }
    }
}
