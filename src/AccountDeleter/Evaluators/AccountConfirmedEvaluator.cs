// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using Microsoft.Extensions.Logging;
using NuGet.Services.Entities;

namespace NuGetGallery.AccountDeleter
{
    public class AccountConfirmedEvaluator : BaseUserEvaluator
    {
        private readonly ILogger<AccountConfirmedEvaluator> _logger;

        public AccountConfirmedEvaluator(ILogger<AccountConfirmedEvaluator> logger)
            : base()
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public override bool CanUserBeDeleted(User user)
        {
            if (user.Confirmed)
            {
                _logger.LogWarning("{Evaluator}:{EvaluatorId} User cannot be deleted because their account is confirmed.", nameof(AccountConfirmedEvaluator), EvaluatorId);
                return false;
            }

            return true;
        }
    }
}
