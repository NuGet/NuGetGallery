// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Linq;
using Microsoft.Extensions.Logging;

namespace NuGet.Services.Metadata.Catalog.Monitoring
{
    public static class ILoggerFactoryExtensions
    {
        /// <summary>
        /// Replacement for <see cref="LoggerFactoryExtensions.CreateLogger(ILoggerFactory, Type)"/> that creates an <see cref="ILogger{TCategoryName}"/>.
        /// </summary>
        public static ILogger CreateTypedLogger(this ILoggerFactory loggerFactory, Type type)
        {
            var typedCreateLoggerMethod =
                typeof(LoggerFactoryExtensions)
                .GetMethods()
                .SingleOrDefault(m =>
                    m.Name == nameof(LoggerFactoryExtensions.CreateLogger) &&
                    m.IsGenericMethod);

            return typedCreateLoggerMethod
                .MakeGenericMethod(type)
                .Invoke(null, new object[] { loggerFactory }) as ILogger;
        }
    }
}
