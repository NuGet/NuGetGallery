// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using Microsoft.Extensions.Logging;
using NuGet.Services.Entities;

namespace NuGetGallery.AccountDeleter
{
    /// <summary>
    /// Evaluator that always passes
    /// </summary>
    public class AlwaysAllowEvaluator : BaseUserEvaluator
    {
        private readonly ILogger<AlwaysAllowEvaluator> _logger;

        public AlwaysAllowEvaluator(ILogger<AlwaysAllowEvaluator> logger)
            : base()
        {
            _logger = logger;
        }

        public override bool CanUserBeDeleted(User user)
        {
            _logger.LogInformation("{Evaluator} User can be deleted. This evaluator always allows deletion.", nameof(AlwaysAllowEvaluator));
            return true;
        }

    }
}
