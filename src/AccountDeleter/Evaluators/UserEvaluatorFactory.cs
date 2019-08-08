// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using Microsoft.Extensions.Options;
using System;
using System.Collections.Generic;

namespace NuGetGallery.AccountDeleter
{
    public class UserEvaluatorFactory : IUserEvaluatorFactory
    {
        private IOptionsSnapshot<AccountDeleteConfiguration> _options;
        private Dictionary<string, IUserEvaluator> _evaluatorMap;

        public UserEvaluatorFactory(IOptionsSnapshot<AccountDeleteConfiguration> options)
        {
            _options = options ?? throw new ArgumentNullException(nameof(options));
            _evaluatorMap = new Dictionary<string, IUserEvaluator>();
        }

        public IUserEvaluator GetEvaluatorForSource(string source)
        {
            var configuration = _options.Value;

            var sourceConfig = configuration.GetSourceConfiguration(source);

            var configEvaluators = sourceConfig.Evaluators;

            if (configEvaluators.Count > 1)
            {
                var result = new AggregateEvaluator();
                foreach(var evaluatorKey in configEvaluators)
                {
                    IUserEvaluator userEvaluator;
                    if(!_evaluatorMap.TryGetValue(evaluatorKey, out userEvaluator))
                    {
                        throw new UnknownEvaluatorException(evaluatorKey, source);
                    }

                    result.AddEvaluator(userEvaluator);
                }

                return result;
            }
            else
            {
                var evaluatorKey = configEvaluators[0];
                IUserEvaluator userEvaluator;
                if (!_evaluatorMap.TryGetValue(evaluatorKey, out userEvaluator))
                {
                    throw new UnknownEvaluatorException(evaluatorKey, source);
                }

                return userEvaluator;
            }
        }

        public bool AddEvaluatorByKey(string key, IUserEvaluator evaluator)
        {
            if (_evaluatorMap.ContainsKey(key))
            {
                return false;
            }

            _evaluatorMap.Add(key, evaluator);
            return true;
        }
    }
}
