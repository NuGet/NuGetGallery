// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using Microsoft.Extensions.Logging;
using NuGet.Protocol;
using NuGet.Protocol.Core.Types;

namespace NuGet.Services.Metadata.Catalog.Monitoring
{
    /// <summary>
    /// Creates <see cref="IValidator"/>s using <see cref="SourceRepository"/>s.
    /// </summary>
    public class ValidatorFactory
    {
        private readonly IDictionary<FeedType, SourceRepository> _feedToSource;
        private readonly ValidatorConfiguration _validatorConfig;
        private readonly ILoggerFactory _loggerFactory;

        /// <summary>
        /// Create a new validator factory.
        /// </summary>
        /// <param name="feedToSource">Used to map <see cref="FeedType"/> to the <see cref="SourceRepository"/> to use.</param>
        /// <param name="validatorConfig">The config that will be passed to <see cref="IValidator"/>s.</param>
        /// <param name="loggerFactory">The factory used to create <see cref="IValidator"/>s' loggers.</param>
        public ValidatorFactory(
            IDictionary<FeedType, SourceRepository> feedToSource,
            ValidatorConfiguration validatorConfig,
            ILoggerFactory loggerFactory)
        {
            _feedToSource = feedToSource ?? throw new ArgumentNullException(nameof(feedToSource));
            _validatorConfig = validatorConfig ?? throw new ArgumentNullException(nameof(validatorConfig));
            _loggerFactory = loggerFactory ?? throw new ArgumentNullException(nameof(loggerFactory));
        }

        public IValidator Create(Type validatorType)
        {
            if (validatorType == null)
            {
                throw new ArgumentNullException(nameof(validatorType));
            }

            var loggerType = typeof(ILogger<>).MakeGenericType(validatorType);

            var constructor = validatorType.GetConstructor(new Type[]
            {
                typeof(IDictionary<FeedType, SourceRepository>),
                typeof(ValidatorConfiguration),
                loggerType
            });

            if (constructor == null)
            {
                throw new Exception(
                    $"Could not initialize {validatorType.Name}! " +
                    $"{validatorType.Name} must have a public constructor that accepts an " +
                    $"{nameof(IDictionary<FeedType, SourceRepository>)}, a {nameof(ValidatorConfiguration)}, and a {loggerType.Name}!");
            }

            return constructor.Invoke(new object[]
            {
                _feedToSource,
                _validatorConfig,
                _loggerFactory.CreateTypedLogger(validatorType)
            }) as IValidator;
        }
    }
}