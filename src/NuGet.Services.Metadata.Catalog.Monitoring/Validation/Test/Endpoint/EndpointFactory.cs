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
        private ValidatorFactory _validationFactory;
        private Func<HttpMessageHandler> _messageHandlerFactory;
        private ILoggerFactory _loggerFactory;

        public class Input
        {
            public Input(string name, Uri cursorUri)
            {
                Name = name;
                CursorUri = cursorUri;
            }

            public string Name { get; }
            public Uri CursorUri { get; }
        }

        public EndpointFactory(
            ValidatorFactory validationFactory,
            Func<HttpMessageHandler> messageHandlerFactory, 
            ILoggerFactory loggerFactory)
        {
            _validationFactory = validationFactory;
            _messageHandlerFactory = messageHandlerFactory;
            _loggerFactory = loggerFactory;
        }

        /// <summary>
        /// Creates the <see cref="EndpointValidator"/> for which <see cref="Type.Name"/> is equal to <paramref name="endpointName"/>.
        /// </summary>
        public EndpointValidator Create(Input input)
        {
            try
            {
                /// Find the <see cref="EndpointValidator"/> named after <param name="endpointName"/>
                var endpointType = Assembly
                    .GetExecutingAssembly()
                    .GetTypes()
                    .FirstOrDefault(t => t.Name == input.Name);

                /// Create a <see cref="HttpReadCursor"/> from the <see cref="Uri"/> for this <see cref="EndpointValidator"/>.
                var endpointCursor = new HttpReadCursor(input.CursorUri, _messageHandlerFactory);

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
                        $"{loggerType.Name}!", nameof(input.Name));
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
                throw new Exception($"Failed to create endpoint named {input.Name}!", e);
            }
        }
    }
}
