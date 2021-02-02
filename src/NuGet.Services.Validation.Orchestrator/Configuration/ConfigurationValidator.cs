// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Configuration;
using System.Linq;
using Microsoft.Extensions.Options;

namespace NuGet.Services.Validation.Orchestrator
{
    /// <summary>
    /// Provides a methods for checking configuration validity
    /// </summary>
    public class ConfigurationValidator
    {
        private readonly IValidatorProvider _validatorProvider;
        private readonly ValidationConfiguration _configuration;

        public ConfigurationValidator(
            IValidatorProvider validatorProvider,
            IOptionsSnapshot<ValidationConfiguration> optionsAccessor)
        {
            if (optionsAccessor == null)
            {
                throw new ArgumentNullException(nameof(optionsAccessor));
            }

            _validatorProvider = validatorProvider ?? throw new ArgumentNullException(nameof(validatorProvider));
            _configuration = optionsAccessor.Value ?? throw new ArgumentException("Value property cannot be null", nameof(optionsAccessor));
        }

        /// <summary>
        /// Checks if configuration object is valid
        /// </summary>
        public void Validate()
        {
            CheckValidationsNumber();

            CheckPropertyValues();

            CheckDuplicateValidations();

            CheckUnknownPrerequisites();

            CheckUnknownValidators();

            CheckForCyclesAndParallelProcessors();

            CheckForUnrunnableRequiredValidations();
        }

        private void CheckValidationsNumber()
        {
            if (_configuration.Validations == null || !_configuration.Validations.Any())
            {
                throw new ConfigurationErrorsException("Must have at least one validation declared");
            }
        }

        private void CheckPropertyValues()
        {
            foreach (var validationConfigurationItem in _configuration.Validations)
            {
                if (string.IsNullOrWhiteSpace(validationConfigurationItem.Name))
                {
                    throw new ConfigurationErrorsException("Validation name cannot be empty");
                }

                if (validationConfigurationItem.TrackAfter == TimeSpan.Zero)
                {
                    throw new ConfigurationErrorsException($"{nameof(validationConfigurationItem.TrackAfter)} must be set for validation {validationConfigurationItem.Name}");
                }
            }
        }

        private void CheckDuplicateValidations()
        {
            var duplicateValidations = _configuration.Validations
                .Select(v => v.Name)
                .GroupBy(n => n)
                .Where(g => g.Count() > 1)
                .ToList();
            if (duplicateValidations.Any())
            {
                throw new ConfigurationErrorsException($"Duplicate validations: {string.Join(", ", duplicateValidations.Select(d => d.Key))}");
            }
        }

        private void CheckUnknownPrerequisites()
        {
            var declaredValidations = new HashSet<string>(_configuration.Validations.Select(v => v.Name));
            var prerequisites = new HashSet<string>(_configuration.Validations.Select(v => v.RequiredValidations).SelectMany(p => p));
            prerequisites.ExceptWith(declaredValidations);
            if (prerequisites.Any())
            {
                throw new ConfigurationErrorsException($"Unknown validations set as prerequisites: {string.Join(", ", prerequisites)}");
            }
        }

        private void CheckUnknownValidators()
        {
            foreach (var validatorItem in _configuration.Validations)
            {
                if (!_validatorProvider.IsNuGetValidator(validatorItem.Name))
                {
                    throw new ConfigurationErrorsException("Validator implementation not found for " + validatorItem.Name);
                }
            }
        }

        private void CheckForUnrunnableRequiredValidations()
        {
            // checks for the case when validation that must run depends on a validation that 
            // is configured not to run
            // we'll just walk up the dependency chain of each runnable validation and look for 
            // not runnable validations

            var validations = _configuration.Validations.ToDictionary(v => v.Name);
            var runnableValidations = _configuration.Validations.Where(v => v.ShouldStart);

            foreach (var validation in runnableValidations)
            {
                var checkQueue = new Queue<string>(validation.RequiredValidations);
                while (checkQueue.Any())
                {
                    var requiredValidationName = checkQueue.Dequeue();
                    var requiredValidation = validations[requiredValidationName];
                    if (!requiredValidation.ShouldStart)
                    {
                        throw new ConfigurationErrorsException($"Runnable validation {validation.Name} cannot be run because it requires non-runnable validation {requiredValidationName} to complete before it can be started.");
                    }
                    requiredValidation.RequiredValidations.ForEach(checkQueue.Enqueue);
                }
            }
        }

        private void CheckForCyclesAndParallelProcessors()
        {
            var processorNames = _configuration
                .Validations
                .Select(x => x.Name)
                .Where(x => _validatorProvider.IsNuGetProcessor(x))
                .ToList();

            TopologicalSort.Validate(_configuration.Validations, processorNames);
        }
    }
}
