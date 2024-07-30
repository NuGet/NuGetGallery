// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using Microsoft.Extensions.Logging;

namespace NuGet.Services.Metadata.Catalog.Monitoring
{
    /// <summary>
    /// Runs a set of <see cref="IValidator{T}"/>s.
    /// </summary>
    /// <typeparam name="T">An <see cref="IEndpoint"/> that the <see cref="IValidator"/>s ran by this <see cref="IAggregateValidator"/> must be associated with.</typeparam>
    public class EndpointValidator<T> : AggregateValidator where T : class, IEndpoint
    {
        public EndpointValidator(
            T endpoint,
            IEnumerable<IValidator<T>> validators, 
            ILogger<AggregateValidator> logger) : 
            base(validators, logger)
        {
            Endpoint = endpoint ?? throw new ArgumentNullException(nameof(endpoint));
        }

        private T Endpoint { get; }
    }
}
