// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Threading.Tasks;
using Microsoft.Extensions.Options;
using NuGet.Jobs.Validation;

namespace NuGet.Services.Validation.Orchestrator
{
    [ValidatorName(Name)]
    public class LocalValidator : BaseNuGetValidator, INuGetValidator
    {
        public const string Name = "LocalValidator";

        private readonly LocalValidationConfiguration _configuration;

        public LocalValidator(IOptionsSnapshot<LocalValidationConfiguration> configurationAccessor)
        {
            if (configurationAccessor == null)
            {
                throw new ArgumentNullException(nameof(configurationAccessor));
            }

            _configuration = configurationAccessor.Value
                ?? throw new ArgumentException("Value property cannot be null", nameof(configurationAccessor));
        }

        public async Task<INuGetValidationResponse> StartAsync(INuGetValidationRequest request)
        {
            ValidateRequest(request);
            await Task.Delay(_configuration.Delay);
            return NuGetValidationResponse.Succeeded;
        }

        public Task<INuGetValidationResponse> GetResponseAsync(INuGetValidationRequest request)
        {
            ValidateRequest(request);
            return Task.FromResult(NuGetValidationResponse.NotStarted);
        }

        private void ValidateRequest(INuGetValidationRequest request)
        {
            if (request == null)
            {
                throw new ArgumentNullException(nameof(request));
            }

            if (!_configuration.Enabled)
            {
                throw new InvalidOperationException("The local validator is not enabled.");
            }

            if (_configuration.Delay < TimeSpan.Zero)
            {
                throw new InvalidOperationException("The local validator delay cannot be negative.");
            }
        }
    }
}
