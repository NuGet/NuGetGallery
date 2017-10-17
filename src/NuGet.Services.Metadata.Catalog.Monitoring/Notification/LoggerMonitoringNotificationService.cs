// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json.Linq;

namespace NuGet.Services.Metadata.Catalog.Monitoring
{
    /// <summary>
    /// <see cref="IMonitoringNotificationService"/> that logs all validation results to an <see cref="ILogger"/>.
    /// </summary>
    public class LoggerMonitoringNotificationService : IMonitoringNotificationService
    {
        private ILogger<LoggerMonitoringNotificationService> _logger;

        public LoggerMonitoringNotificationService(ILogger<LoggerMonitoringNotificationService> logger)
        {
            _logger = logger;
        }

        public Task OnPackageValidationFinishedAsync(PackageValidationResult result, CancellationToken token)
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
                            _logger.LogError(LogEvents.ValidationFailed, validationResult.Exception, testResultLogString, result.Package.Id, result.Package.Version, validatorName, testResultString);
                            break;
                    }
                }
            }

            return Task.FromResult(0);
        }

        public Task OnPackageValidationFailedAsync(string packageId, string packageVersion, Exception e, CancellationToken token)
        {
            _logger.LogError(LogEvents.ValidationFailedToRun, e, "Failed to test {PackageId} {PackageVersion}!", packageId, packageVersion);

            return Task.FromResult(0);
        }
    }
}
