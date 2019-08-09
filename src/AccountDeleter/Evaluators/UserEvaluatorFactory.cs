// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using Microsoft.Extensions.Options;
using System;
using System.Collections.Generic;
using System.Web.UI;

namespace NuGetGallery.AccountDeleter
{
    public class UserEvaluatorFactory : IUserEvaluatorFactory
    {
        private IOptionsSnapshot<AccountDeleteConfiguration> _options;
        private Func<EvaluatorKey, IUserEvaluator> _func;

        public UserEvaluatorFactory(IOptionsSnapshot<AccountDeleteConfiguration> options, Func<EvaluatorKey, IUserEvaluator> func)
        {
            _options = options ?? throw new ArgumentNullException(nameof(options));
            _func = func ?? throw new ArgumentNullException(nameof(func));
        }

        public IUserEvaluator GetEvaluatorForSource(string source)
        {
            var configuration = _options.Value;
            var sourceConfig = configuration.GetSourceConfiguration(source);

            var configEvaluators = sourceConfig.Evaluators;
            var result = new AggregateEvaluator();
            foreach(var evaluatorKey in configEvaluators)
            {
                result.AddEvaluator(_func(evaluatorKey));
            }

            return result;
        }
    }
}
