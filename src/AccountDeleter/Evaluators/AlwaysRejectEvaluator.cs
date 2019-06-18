// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using Microsoft.Extensions.Logging;
using NuGet.Services.Entities;
using System;

namespace NuGetGallery.AccountDeleter
{
    public class AlwaysRejectEvaluator : IUserEvaluator
    {
        private readonly Guid _id;
        private readonly ILogger<AlwaysRejectEvaluator> _logger;

        public AlwaysRejectEvaluator(ILogger<AlwaysRejectEvaluator> logger)
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
            _logger.LogWarning("{Evaluator}:{EvaluatorId} User cannot be deleted because this evaluator always disallows delete.", nameof(AlwaysRejectEvaluator), EvaluatorId);
            return false;
        }
    }
}
