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
        private readonly ValidationConfiguration _configuration;

        public ConfigurationValidator(IOptionsSnapshot<ValidationConfiguration> optionsAccessor)
        {
            if (optionsAccessor == null)
            {
                throw new ArgumentNullException(nameof(optionsAccessor));
            }

            _configuration = optionsAccessor.Value ?? throw new ArgumentException("Value property cannot be null", nameof(optionsAccessor));
        }

        /// <summary>
        /// Checks if configuration object is valid
        /// </summary>
        public void Validate()
        {
            CheckValidationsNumber(_configuration.Validations);

            CheckPropertyValues(_configuration);

            CheckDuplicateValidations(_configuration);

            CheckUnknownPrerequisites(_configuration);

            CheckPrerequisitesLoops(_configuration);
        }

        private static void CheckValidationsNumber(List<ValidationConfigurationItem> validations)
        {
            if (validations == null || !validations.Any())
            {
                throw new ConfigurationErrorsException("Must have at least one validation declared");
            }
        }

        private static void CheckPropertyValues(ValidationConfiguration configuration)
        {
            foreach (var validationConfigurationItem in configuration.Validations)
            {
                if (string.IsNullOrWhiteSpace(validationConfigurationItem.Name))
                {
                    throw new ConfigurationErrorsException("Validation name cannot be empty");
                }

                if (validationConfigurationItem.FailAfter == TimeSpan.Zero)
                {
                    throw new ConfigurationErrorsException($"failAfter timeout must be set for validation {validationConfigurationItem.Name}");
                }
            }
        }

        private static void CheckDuplicateValidations(ValidationConfiguration configuration)
        {
            var duplicateValidations = configuration.Validations
                .Select(v => v.Name)
                .GroupBy(n => n)
                .Where(g => g.Count() > 1)
                .ToList();
            if (duplicateValidations.Any())
            {
                throw new ConfigurationErrorsException($"Duplicate validations: {string.Join(", ", duplicateValidations.Select(d => d.Key))}");
            }
        }

        private static void CheckUnknownPrerequisites(ValidationConfiguration configuration)
        {
            var declaredValidations = new HashSet<string>(configuration.Validations.Select(v => v.Name));
            var prerequisites = new HashSet<string>(configuration.Validations.Select(v => v.RequiredValidations).SelectMany(p => p));
            prerequisites.ExceptWith(declaredValidations);
            if (prerequisites.Any())
            {
                throw new ConfigurationErrorsException($"Unknown validations set as prerequisites: {string.Join(", ", prerequisites)}");
            }
        }

        private static void CheckPrerequisitesLoops(ValidationConfiguration configuration)
        {
            var validations = configuration.Validations.ToDictionary(v => v.Name);

            var globalVisitedValidations = new HashSet<string>();
            foreach (var validationName in validations.Keys)
            {
                ValidationDepthFirstSearch(validationName, new HashSet<string>(), globalVisitedValidations, validations);
            }
        }

        /// <summary>
        /// Runs depth first search across validations starting at specified validation.
        /// Throws <see cref="ConfigurationErrorsException"> if it finds an opportunity to visit a validation that was 
        /// visited in current call stack
        /// </summary>
        /// <param name="validationName">Current validation name</param>
        /// <param name="callStackValidations">
        /// Hashset where validations visited in current call stack are tracked.
        /// Must be empty when called from outside.
        /// </param>
        /// <param name="globalVisitedList">
        /// Global list of visited validations across all calls.
        /// Must be empty on the very first call to the <see cref="ValidationDepthFirstSearch"/>, and kept between subsequent calls.
        /// </param>
        /// <param name="validationItems">Map of the validation name to that validation configuration.</param>
        private static void ValidationDepthFirstSearch(string validationName, HashSet<string> callStackValidations, HashSet<string> globalVisitedList, IReadOnlyDictionary<string, ValidationConfigurationItem> validationItems)
        {
            callStackValidations.Add(validationName);
            globalVisitedList.Add(validationName);

            var currentValidation = validationItems[validationName];

            foreach (var validationDependency in currentValidation.RequiredValidations)
            {
                if (!globalVisitedList.Contains(validationDependency))
                {
                    ValidationDepthFirstSearch(validationDependency, callStackValidations, globalVisitedList, validationItems);
                }
                else if (callStackValidations.Contains(validationDependency))
                {
                    throw new ConfigurationErrorsException($"Validation dependency loop detected at {validationName}");
                }
            }

            callStackValidations.Remove(validationName);
        }
    }
}
