// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Concurrent;
using System.Threading.Tasks;
using Microsoft.Extensions.Options;
using NuGet.Jobs.Validation;

namespace NuGet.Services.Validation.Orchestrator
{
    /// <summary>
    /// Simulates validation for local development and always completes successfully after the configured delay.
    /// </summary>
    [ValidatorName(Name)]
    public class AlwaysSucceedingValidator : BaseNuGetValidator, INuGetValidator
    {
        public const string Name = "AlwaysSucceedingValidator";

        private readonly AlwaysSucceedingValidatorConfiguration _configuration;
        private readonly ConcurrentDictionary<Guid, DateTimeOffset> _startedValidations = new ConcurrentDictionary<Guid, DateTimeOffset>();

        public AlwaysSucceedingValidator(IOptions<AlwaysSucceedingValidatorConfiguration> configurationAccessor)
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

            if (_configuration.Delay < TimeSpan.Zero)
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

            var response = DateTimeOffset.UtcNow - started < _configuration.Delay
                ? NuGetValidationResponse.Incomplete
                : NuGetValidationResponse.Succeeded;

            return Task.FromResult(response);
        }
    }
}
