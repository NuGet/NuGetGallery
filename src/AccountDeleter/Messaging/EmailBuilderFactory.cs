// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NuGet.Services.Messaging.Email;
using System;

namespace NuGetGallery.AccountDeleter
{
    public class EmailBuilderFactory : IEmailBuilderFactory
    {
        private readonly IOptionsSnapshot<AccountDeleteConfiguration> _options;
        private readonly ILogger<EmailBuilderFactory> _logger;

        public EmailBuilderFactory(
            IOptionsSnapshot<AccountDeleteConfiguration> options,
            ILogger<EmailBuilderFactory> logger)
        {
            _options = options ?? throw new ArgumentNullException(nameof(options));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public IEmailBuilder GetEmailBuilder(string source)
        {
            var options = _options.Value;

            return options.GetEmailBuilder(source);
        }
    }
}
