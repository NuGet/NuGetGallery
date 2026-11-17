// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Concurrent;
using System.Threading.Tasks;
using Microsoft.Extensions.Options;
using NuGet.Jobs.Validation;
using NuGet.Services.Validation.Issues;

namespace NuGet.Services.Validation.Orchestrator
{
    /// <summary>
    /// Simulates successful and failed validation outcomes for local development.
    /// </summary>
    [ValidatorName(Name)]
    public class DevelopmentValidator : BaseNuGetValidator, INuGetValidator
    {
        public const string Name = "DevelopmentValidator";

        private readonly DevelopmentValidatorConfiguration _configuration;
        private readonly ConcurrentDictionary<Guid, DateTimeOffset> _startedValidations = new ConcurrentDictionary<Guid, DateTimeOffset>();

        public DevelopmentValidator(IOptions<DevelopmentValidatorConfiguration> configurationAccessor)
        {
            if (configurationAccessor == null)
            {
                throw new ArgumentNullException(nameof(configurationAccessor));
            }

            _configuration = configurationAccessor.Value
                ?? throw new ArgumentException("Value property cannot be null", nameof(configurationAccessor));

            if (!_configuration.Enabled)
            {
                throw new InvalidOperationException("The always succeeding validator is not enabled.");
            }

            if (_configuration.DelaySeconds < 0)
            {
                throw new InvalidOperationException("The always succeeding validator delay cannot be negative.");
            }
        }

        public Task<INuGetValidationResponse> StartAsync(INuGetValidationRequest request)
        {
            if (request == null)
            {
                throw new ArgumentNullException(nameof(request));
            }

            _startedValidations.TryAdd(request.ValidationId, DateTimeOffset.UtcNow);
            return Task.FromResult(NuGetValidationResponse.Incomplete);
        }

        public Task<INuGetValidationResponse> GetResponseAsync(INuGetValidationRequest request)
        {
            if (request == null)
            {
                throw new ArgumentNullException(nameof(request));
            }

            if (!_startedValidations.TryGetValue(request.ValidationId, out var started))
            {
                return Task.FromResult(NuGetValidationResponse.NotStarted);
            }

            var response = DateTimeOffset.UtcNow - started < TimeSpan.FromSeconds(_configuration.DelaySeconds)
                ? NuGetValidationResponse.Incomplete
                : GetTerminalResponse(request);

            return Task.FromResult(response);
        }

        private INuGetValidationResponse GetTerminalResponse(INuGetValidationRequest request)
        {
            if (!string.IsNullOrEmpty(_configuration.FailurePackageIdPrefix)
                && request.PackageId.StartsWith(_configuration.FailurePackageIdPrefix, StringComparison.OrdinalIgnoreCase))
            {
                return NuGetValidationResponse.FailedWithIssues(ValidationIssue.Unknown);
            }

            return NuGetValidationResponse.Succeeded;
        }
    }
}
