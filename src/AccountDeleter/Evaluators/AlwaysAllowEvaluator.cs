// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using Microsoft.Extensions.Logging;
using NuGet.Services.Entities;
using System;

namespace NuGetGallery.AccountDeleter
{
    /// <summary>
    /// Evaluator that always passes
    /// </summary>
    public class AlwaysAllowEvaluator : IUserEvaluator
    {
        private readonly Guid _id;
        private readonly ILogger<AlwaysAllowEvaluator> _logger;

        public AlwaysAllowEvaluator(ILogger<AlwaysAllowEvaluator> logger)
        {
            _id = Guid.NewGuid();
            _logger = logger;
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
            _logger.LogInformation("{Evaluator} User can be deleted", nameof(AlwaysAllowEvaluator));
            return true;
        }

    }
}
