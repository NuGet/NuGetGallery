﻿// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using Microsoft.Extensions.Logging;
using NuGet.Services.Entities;
using System;
using System.Collections.Generic;
using System.Linq;

namespace NuGetGallery.AccountDeleter
{
    /// <summary>
    /// Evaluator that runs other evaluators and returns a single result. Runs "AND" logic between evaluators.
    /// </summary>
    public class AggregateEvaluator : IUserEvaluator
    {
        private readonly Guid _id;
        private readonly ILogger<AggregateEvaluator> _logger;
        private Dictionary<string, IUserEvaluator> _evaluatorList;

        public AggregateEvaluator(ILogger<AggregateEvaluator> logger)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _evaluatorList = new Dictionary<string, IUserEvaluator>();
            _id = Guid.NewGuid();
        }

        public string EvaluatorId
        {
            get
            {
                return _id.ToString();
            }
        }

        public bool CanUserBeDeleted(User user)
        {
            var evaluators = _evaluatorList.Values;
            _logger.LogInformation("Running {EvaluatorCount} evaluators.", evaluators.Count);
            return evaluators.All(e => e.CanUserBeDeleted(user));
        }

        public bool AddEvaluator(IUserEvaluator userEvaluator)
        {
            if (_evaluatorList.ContainsKey(userEvaluator.EvaluatorId))
            {
                _logger.LogWarning("Evaluator {EvaluatorId} was attempted to be added to aggregate more than once!", userEvaluator.EvaluatorId);
                return false;
            }

            _logger.LogInformation("Adding evaluator {EvaluatorName} with id {EvaluatorId} to aggregate.", userEvaluator.GetType().FullName, userEvaluator.EvaluatorId);
            _evaluatorList.Add(userEvaluator.EvaluatorId, userEvaluator);

            return true;
        }
    }
}
