// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json.Linq;

namespace NuGet.Services.Metadata.Catalog.Monitoring
{
    /// <summary>
    /// Default <see cref="IMonitoringNotificationService"/> that logs all information to an <see cref="ILogger"/>.
    /// </summary>
    public class LoggerMonitoringNotificationService : IMonitoringNotificationService
    {
        private ILogger<LoggerMonitoringNotificationService> _logger;

        public LoggerMonitoringNotificationService(ILogger<LoggerMonitoringNotificationService> logger)
        {
            _logger = logger;
        }

        public void OnPackageValidationFinished(PackageValidationResult result)
        {
            _logger.LogInformation("Finished testing {PackageId} {PackageVersion}", result.Package.Id, result.Package.Version);
            var groupedResults = result.AggregateValidationResults.SelectMany(r => r.ValidationResults).GroupBy(r => r.Result);
            foreach (var resultsWithResult in groupedResults)
            {
                foreach (var validationResult in resultsWithResult)
                {
                    var testResultLogString = "{PackageId} {PackageVersion}: {ValidatorName}: {TestResult}";
                    var validatorName = validationResult.Validator.GetType().Name;
                    var testResultString = validationResult.Result.ToString();

                    switch (validationResult.Result)
                    {
                        case TestResult.Pass:
                        case TestResult.Skip:
                            _logger.LogInformation(testResultLogString, result.Package.Id, result.Package.Version, validatorName, testResultString);
                            break;
                        case TestResult.Fail:
                            _logger.LogError(LogEvents.ValidationFailed, validationResult.Exception, testResultLogString + ": {ExceptionMessage}", result.Package.Id, result.Package.Version, validatorName, testResultString, validationResult.Exception.Message);
                            break;
                    }
                }
            }
        }

        public void OnPackageValidationFailed(string packageId, string packageVersion, IList<JObject> catalogEntriesJson, Exception e)
        {
            _logger.LogError(LogEvents.ValidationFailedToRun, e, "Failed to test {PackageId} {PackageVersion}! {ExceptionMessage}", packageId, packageVersion, e.Message);
        }
    }
}
