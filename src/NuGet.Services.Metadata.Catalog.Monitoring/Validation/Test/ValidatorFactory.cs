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
        private readonly ILoggerFactory _loggerFactory;

        /// <param name="feedToSource">Used to map <see cref="FeedType"/> to the <see cref="SourceRepository"/> to use.</param>
        public ValidatorFactory(IDictionary<FeedType, SourceRepository> feedToSource, ILoggerFactory loggerFactory)
        {
            _feedToSource = feedToSource;
            _loggerFactory = loggerFactory;
        }

        public IValidator Create(Type validatorType)
        {
            var loggerType = typeof(ILogger<>).MakeGenericType(validatorType);

            var constructor = validatorType.GetConstructor(new Type[] 
            {
                typeof(IDictionary<FeedType, SourceRepository>),
                loggerType
            });

            if (constructor == null)
            {
                throw new Exception(
                    $"Could not initialize {validatorType.Name}! " +
                    $"{validatorType.Name} must have a public constructor that accepts an " +
                    $"{nameof(IDictionary<FeedType, SourceRepository>)} and a {loggerType.Name}!");
            }

            return constructor.Invoke(new object[] 
            {
                _feedToSource,
                _loggerFactory.CreateTypedLogger(validatorType)
            }) as IValidator;
        }
    }
}
