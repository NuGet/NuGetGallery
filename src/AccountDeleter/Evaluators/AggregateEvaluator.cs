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
        private HashSet<IUserEvaluator> _evaluatorList = new HashSet<IUserEvaluator>(new UserEvaluatorComparer());

        public override bool CanUserBeDeleted(User user)
        {
            return _evaluatorList.All(e => e.CanUserBeDeleted(user));
        }

        public bool AddEvaluator(IUserEvaluator userEvaluator)
        {
            return _evaluatorList.Add(userEvaluator);
        }
    }
}