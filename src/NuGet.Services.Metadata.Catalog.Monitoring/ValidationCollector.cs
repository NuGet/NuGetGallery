// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json.Linq;
using NuGet.Services.Metadata.Catalog.Persistence;
using NuGet.Services.Metadata.Catalog.Helpers;

namespace NuGet.Services.Metadata.Catalog.Monitoring
{
    /// <summary>
    /// Runs a <see cref="PackageValidator"/> against collected packages.
    /// </summary>
    public class ValidationCollector : SortingIdVersionCollector
    {
        public ValidationCollector(
            Storage auditingStorage, 
            PackageValidator packageValidator, 
            Uri index,
            ILogger<ValidationCollector> logger,
            Func<HttpMessageHandler> handlerFunc = null) 
            : base(index, handlerFunc)
        {
            _auditingStorage = auditingStorage ?? throw new ArgumentNullException(nameof(auditingStorage));
            _packageValidator = packageValidator ?? throw new ArgumentNullException(nameof(packageValidator));
            _logger = logger;
        }

        private readonly Storage _auditingStorage;
        private readonly PackageValidator _packageValidator;
        
        private readonly ILogger<ValidationCollector> _logger;

        protected override async Task ProcessSortedBatch(CollectorHttpClient client, KeyValuePair<FeedPackageIdentity, IList<JObject>> sortedBatch, JToken context, CancellationToken cancellationToken)
        {
            var packageId = sortedBatch.Key.Id;
            var packageVersion = sortedBatch.Key.Version;
            var catalogEntriesJson = sortedBatch.Value;

            try
            {
                var validationResults = await _packageValidator.Validate(packageId, packageVersion, catalogEntriesJson, _auditingStorage, client, cancellationToken);
                _logger.LogInformation("Finished testing {PackageId} {PackageVersion}", packageId, packageVersion);
                var groupedResults = validationResults.AggregateValidationResults.SelectMany(r => r.ValidationResults).GroupBy(r => r.Result);
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
                                _logger.LogInformation(testResultLogString, packageId, packageVersion, validatorName, testResultString);
                                break;
                            case TestResult.Fail:
                                _logger.LogError(LogEvents.ValidationFailed, validationResult.Exception, testResultLogString + ": {ExceptionMessage}", packageId, packageVersion, validatorName, testResultString, validationResult.Exception.Message);
                                break;
                        }
                    }
                }
            }
            catch (Exception e)
            {
                _logger.LogError(LogEvents.ValidationFailedToRun, e, "Failed to test {PackageId} {PackageVersion}! {ExceptionMessage}", packageId, packageVersion, e.Message);
            }
        }
    }
}
