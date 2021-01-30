// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using NuGet.Jobs.Validation;

namespace NuGet.Services.Validation.Orchestrator
{
    public class ValidatorProvider : IValidatorProvider
    {
        /// <summary>
        /// This is a cache of all of the <see cref="INuGetValidator"/> and <see cref="INuGetProcessor"/> implementations
        /// available.
        /// </summary>
        private static EvaluatedTypes _evaluatedTypes;
        private static object _evaluatedTypesLock = new object();

        private readonly IServiceProvider _serviceProvider;
        private readonly ILogger<ValidatorProvider> _logger;

        public ValidatorProvider(IServiceProvider serviceProvider, ILogger<ValidatorProvider> logger)
        {
            _serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));

            InitializeEvaluatedTypes(Assembly.GetCallingAssembly());
        }

        /// <summary>
        /// Discovers all <see cref="INuGetValidator"/> and <see cref="INuGetProcessor"/> types available and caches the result.
        /// </summary>
        private void InitializeEvaluatedTypes(Assembly callingAssembly)
        {
            if (_evaluatedTypes != null)
            {
                return;
            }

            lock (_evaluatedTypesLock)
            {
                if (_evaluatedTypes != null)
                {
                    return;
                }

                using (_logger.BeginScope($"Enumerating all {nameof(INuGetValidator)} implementations"))
                {
                    _logger.LogTrace("Before enumeration");
                    IEnumerable<Type> candidateTypes = GetCandidateTypes(callingAssembly);

                    var nugetValidatorTypes = candidateTypes
                        .Where(type => typeof(INuGetValidator).IsAssignableFrom(type)
                               && type != typeof(INuGetValidator)
                               && type != typeof(INuGetProcessor)
                               && ValidatorUtility.HasValidatorNameAttribute(type))
                        .ToDictionary(ValidatorUtility.GetValidatorName);

                    var nugetProcessorTypes = nugetValidatorTypes
                        .Values
                        .Where(IsProcessorType)
                        .ToDictionary(ValidatorUtility.GetValidatorName);

                    _logger.LogTrace("After enumeration, got {NumImplementations} implementations: {TypeNames}",
                        nugetValidatorTypes.Count,
                        nugetValidatorTypes.Keys);

                    _evaluatedTypes = new EvaluatedTypes(nugetValidatorTypes, nugetProcessorTypes);
                }
            }
        }

        public bool IsNuGetValidator(string validatorName)
        {
            validatorName = validatorName ?? throw new ArgumentNullException(nameof(validatorName));

            return _evaluatedTypes.NuGetValidatorTypes.ContainsKey(validatorName);
        }

        public bool IsNuGetProcessor(string validatorName)
        {
            validatorName = validatorName ?? throw new ArgumentNullException(nameof(validatorName));

            return _evaluatedTypes.NuGetProcessorTypes.ContainsKey(validatorName);
        }

        public INuGetValidator GetNuGetValidator(string validatorName)
        {
            validatorName = validatorName ?? throw new ArgumentNullException(nameof(validatorName));

            if (_evaluatedTypes.NuGetValidatorTypes.TryGetValue(validatorName, out Type validatorType))
            {
                return (INuGetValidator)_serviceProvider.GetRequiredService(validatorType);
            }

            throw new ArgumentException($"Unknown validator name: {validatorName}", nameof(validatorName));
        }

        private static IEnumerable<Type> GetCandidateTypes(Assembly callingAssembly)
        {
            var executingAssembly = Assembly.GetExecutingAssembly();
            IEnumerable<Type> candidateTypes = executingAssembly.GetTypes();
            if (callingAssembly != executingAssembly)
            {
                candidateTypes = candidateTypes.Concat(callingAssembly.GetTypes());
            }

            return candidateTypes;
        }

        private static bool IsProcessorType(Type type)
        {
            return typeof(INuGetProcessor).IsAssignableFrom(type);
        }

        private class EvaluatedTypes
        {
            public EvaluatedTypes(
                IReadOnlyDictionary<string, Type> nugetValidatorTypes,
                IReadOnlyDictionary<string, Type> nugetProcessorTypes)
            {
                NuGetValidatorTypes = nugetValidatorTypes ?? throw new ArgumentNullException(nameof(nugetValidatorTypes));
                NuGetProcessorTypes = nugetProcessorTypes ?? throw new ArgumentNullException(nameof(nugetProcessorTypes));
            }

            public IReadOnlyDictionary<string, Type> NuGetValidatorTypes { get; }
            public IReadOnlyDictionary<string, Type> NuGetProcessorTypes { get; }
        }
    }
}
