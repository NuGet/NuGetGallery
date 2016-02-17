// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using Correlator.Extensions;
using System;
using System.Net.Http;

namespace NuGet.Services.Search.Client.Correlation
{
    public class CorrelationIdProvider
    {
        public Guid CorrelationId { get; private set; }

        public CorrelationIdProvider()
        {
            CorrelationId = Guid.NewGuid();
        }

        public CorrelationIdProvider(HttpRequestMessage request)
        {
            CorrelationId = request.GetClientCorrelationId();

            if (CorrelationId == Guid.Empty)
            {
                CorrelationId = Guid.NewGuid();
            }
        }
    }
}
