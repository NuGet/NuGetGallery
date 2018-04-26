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
        /// This is a cache of all of the <see cref="IValidator"/> and <see cref="IProcessor"/> implementations
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
        /// Discovers all <see cref="IValidator"/> and <see cref="IProcessor"/> types available and caches the result.
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

                using (_logger.BeginScope("Enumerating all IValidator implementations"))
                {
                    _logger.LogTrace("Before enumeration");
                    IEnumerable<Type> candidateTypes = GetCandidateTypes(callingAssembly);

                    var validatorTypes = candidateTypes
                        .Where(type => typeof(IValidator).IsAssignableFrom(type)
                               && type != typeof(IValidator)
                               && type != typeof(IProcessor)
                               && ValidatorUtility.HasValidatorNameAttribute(type))
                        .ToDictionary(ValidatorUtility.GetValidatorName);

                    var processorTypes = validatorTypes
                        .Values
                        .Where(IsProcessorType)
                        .ToDictionary(ValidatorUtility.GetValidatorName);

                    _logger.LogTrace("After enumeration, got {NumImplementations} implementations: {TypeNames}",
                        validatorTypes.Count,
                        validatorTypes.Keys);

                    _evaluatedTypes = new EvaluatedTypes(validatorTypes, processorTypes);
                }
            }
        }

        public bool IsValidator(string validatorName)
        {
            validatorName = validatorName ?? throw new ArgumentNullException(nameof(validatorName));

            return _evaluatedTypes.ValidatorTypes.ContainsKey(validatorName);
        }

        public bool IsProcessor(string validatorName)
        {
            validatorName = validatorName ?? throw new ArgumentNullException(nameof(validatorName));

            return _evaluatedTypes.ProcessorTypes.ContainsKey(validatorName);
        }

        public IValidator GetValidator(string validatorName)
        {
            validatorName = validatorName ?? throw new ArgumentNullException(nameof(validatorName));

            if (_evaluatedTypes.ValidatorTypes.TryGetValue(validatorName, out Type validatorType))
            {
                return (IValidator)_serviceProvider.GetRequiredService(validatorType);
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
            return typeof(IProcessor).IsAssignableFrom(type);
        }

        private class EvaluatedTypes
        {
            public EvaluatedTypes(
                IReadOnlyDictionary<string, Type> validatorTypes,
                IReadOnlyDictionary<string, Type> processorTypes)
            {
                ValidatorTypes = validatorTypes ?? throw new ArgumentNullException(nameof(validatorTypes));
                ProcessorTypes = processorTypes ?? throw new ArgumentNullException(nameof(validatorTypes));
            }

            public IReadOnlyDictionary<string, Type> ValidatorTypes { get; }
            public IReadOnlyDictionary<string, Type> ProcessorTypes { get; }
        }
    }
}
