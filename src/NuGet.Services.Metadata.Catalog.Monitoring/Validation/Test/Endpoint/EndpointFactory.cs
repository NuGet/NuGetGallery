// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Linq;
using System.Net.Http;
using System.Reflection;
using Microsoft.Extensions.Logging;

namespace NuGet.Services.Metadata.Catalog.Monitoring
{
    /// <summary>
    /// Creates <see cref="EndpointValidator"/>s by name. 
    /// </summary>
    public class EndpointFactory
    {
        public EndpointFactory(
            ValidatorFactory validationFactory, 
            Func<string, Uri> endpointCursorUriFactory, 
            Func<HttpMessageHandler> messageHandlerFactory, 
            ILoggerFactory loggerFactory)
        {
            _validationFactory = validationFactory;
            _endpointCursorUriFactory = endpointCursorUriFactory;
            _messageHandlerFactory = messageHandlerFactory;
            _loggerFactory = loggerFactory;
        }

        private ValidatorFactory _validationFactory;
        private Func<string, Uri> _endpointCursorUriFactory;
        private Func<HttpMessageHandler> _messageHandlerFactory;
        private ILoggerFactory _loggerFactory;

        /// <summary>
        /// Creates the <see cref="EndpointValidator"/> for which <see cref="Type.Name"/> is equal to <paramref name="endpointName"/>.
        /// </summary>
        public EndpointValidator Create(string endpointName)
        {
            try
            {
                /// Find the <see cref="EndpointValidator"/> named after <param name="endpointName"/>
                var endpointType = Assembly
                    .GetExecutingAssembly()
                    .GetTypes()
                    .FirstOrDefault(t => t.Name == endpointName);

                /// Create a <see cref="HttpReadCursor"/> from the <see cref="Uri"/> for this <see cref="EndpointValidator"/>.
                var endpointCursorUri = _endpointCursorUriFactory(endpointName);
                var endpointCursor = new HttpReadCursor(endpointCursorUri, _messageHandlerFactory);

                /// Construct the <see cref="EndpointValidator"/>.
                var loggerType = typeof(ILogger<>).MakeGenericType(endpointType);

                var endpointConstructor = endpointType
                    .GetConstructor(new Type[] 
                    {
                        typeof(ReadCursor),
                        typeof(ValidatorFactory),
                        loggerType
                    });

                if (endpointConstructor == null)
                {
                    throw new ArgumentException($"Endpoint must have a constructor with a " +
                        $"{nameof(ReadCursor)}, " +
                        $"{nameof(ValidatorFactory)}, and " +
                        $"{loggerType.Name}!", nameof(endpointName));
                }

                var endpoint = endpointConstructor.Invoke(new object[] 
                {
                    endpointCursor,
                    _validationFactory,
                    _loggerFactory.CreateTypedLogger(endpointType)
                });

                return endpoint as EndpointValidator;
            }
            catch (Exception e)
            {
                throw new Exception($"Failed to create endpoint named {endpointName}!", e);
            }
        }
    }
}
