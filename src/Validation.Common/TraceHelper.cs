// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using Microsoft.Extensions.Logging;

namespace NuGet.Jobs.Validation.Common
{
    public static class TraceHelper
    {
        /// <summary>
        /// Tracks the result of the validation.
        /// </summary>
        /// <param name="logger">Logger object to use</param>
        /// <param name="validatorName">The name of validator attempted</param>
        /// <param name="validationId">Validation ID of the finished validator</param>
        /// <param name="result">Validation result</param>
        /// <param name="packageId">Package ID</param>
        /// <param name="packageVersion">Package name</param>
        public static void TrackValidatorResult(this ILogger logger, string validatorName, Guid validationId, string result, string packageId, string packageVersion)
        {
            logger.LogInformation($"{{{TraceConstant.EventName}}}: " +
                    $"{{{TraceConstant.ValidatorName}}} " +
                    $"ValidationId: {{{TraceConstant.ValidationId}}} " +
                    $"for package {{{TraceConstant.PackageId}}} " +
                    $"{{{TraceConstant.PackageVersion}}} " +
                    $"resulted in {{Result}}",
                "ValidatorResult",
                validatorName,
                validationId,
                packageId,
                packageVersion,
                result);
        }

        /// <summary>
        /// Tracks the result of the validation with additional information
        /// </summary>
        /// <param name="logger">Logger object to use</param>
        /// <param name="validatorName">The name of validator attempted</param>
        /// <param name="validationId">Validation ID of the finished validator</param>
        /// <param name="result">Validation result</param>
        /// <param name="packageId">Package ID</param>
        /// <param name="packageVersion">Package name</param>
        /// <param name="additionalInformation">Additional information you'd want logged</param>
        public static void TrackValidatorResult(this ILogger logger, string validatorName, Guid validationId, string result, string packageId, string packageVersion, string additionalInformation)
        {
            logger.LogInformation($"{{{TraceConstant.EventName}}}: " +
                    $"{{{TraceConstant.ValidatorName}}} " +
                    $"ValidationId: {{{TraceConstant.ValidationId}}} " +
                    $"for package {{{TraceConstant.PackageId}}} " +
                    $"{{{TraceConstant.PackageVersion}}} " +
                    $"resulted in {{Result}}, " +
                    $"additional info: {{AdditionalInformation}}",
                "ValidatorResult",
                validatorName,
                validationId,
                packageId,
                packageVersion,
                result,
                additionalInformation);
        }

        /// <summary>
        /// Tracks the exception occured during validation
        /// </summary>
        /// <param name="logger">Logger object to use</param>
        /// <param name="validatorName">The name of the validator that was running when exception happened</param>
        /// <param name="validationId">Validation ID that was being processed when exception happened</param>
        /// <param name="ex">The exception to track</param>
        /// <param name="packageId">Package ID</param>
        /// <param name="packageVersion">Package name</param>
        public static void TrackValidatorException(this ILogger logger, string validatorName, Guid validationId, Exception ex, string packageId, string packageVersion)
        {
            logger.LogError(TraceEvent.ValidatorException, ex, 
                    $"{{{TraceConstant.EventName}}} " +
                    $"occurred while running {{{TraceConstant.ValidatorName}}} {{{TraceConstant.ValidationId}}}" +
                    $"on package {{{TraceConstant.PackageId}}}" +
                    $"{{{TraceConstant.PackageVersion}}}", 
                "ValidatorException",
                validationId,
                validatorName,
                packageId,
                packageVersion);
        }
    }
}
