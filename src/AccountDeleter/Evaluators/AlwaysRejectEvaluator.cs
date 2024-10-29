// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using Microsoft.Extensions.Logging;
using NuGet.Services.Entities;

namespace NuGetGallery.AccountDeleter
{
    /// <summary>
    /// Evaluator that always fails
    /// </summary>
    public class AlwaysRejectEvaluator : BaseUserEvaluator
    {
        private readonly ILogger<AlwaysRejectEvaluator> _logger;

        public AlwaysRejectEvaluator(ILogger<AlwaysRejectEvaluator> logger)
            : base()
        {
            _logger = logger;
        }

        public override bool CanUserBeDeleted(User user)
        {
            _logger.LogWarning("{Evaluator}:{EvaluatorId} User cannot be deleted because this evaluator always disallows delete.", nameof(AlwaysRejectEvaluator), EvaluatorId);
            return false;
        }
    }
}
