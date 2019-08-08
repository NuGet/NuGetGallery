// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using NuGet.Services.Entities;
using System.Collections.Generic;
using System.Linq;

namespace NuGetGallery.AccountDeleter
{
    /// <summary>
    /// Evaluator that runs other evaluators and returns a single result. Runs "AND" logic between evaluators.
    /// </summary>
    public class AggregateEvaluator : BaseUserEvaluator
    {
        private Dictionary<string, IUserEvaluator> _evaluatorList;

        public AggregateEvaluator()
            : base()
        {
            _evaluatorList = new Dictionary<string, IUserEvaluator>();
        }

        public override bool CanUserBeDeleted(User user)
        {
            var evaluators = _evaluatorList.Values;
            return evaluators.All(e => e.CanUserBeDeleted(user));
        }

        public bool AddEvaluator(IUserEvaluator userEvaluator)
        {
            if (_evaluatorList.ContainsKey(userEvaluator.EvaluatorId))
            {
                return false;
            }

            _evaluatorList.Add(userEvaluator.EvaluatorId, userEvaluator);
            return true;
        }
    }
}