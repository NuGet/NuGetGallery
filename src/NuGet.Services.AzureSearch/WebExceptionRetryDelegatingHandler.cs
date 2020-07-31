// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace NuGet.Services.AzureSearch
{
    public class WebExceptionRetryDelegatingHandler : DelegatingHandler
    {
        private static readonly HashSet<WebExceptionStatus> _transientWebExceptionStatuses = new HashSet<WebExceptionStatus>(new[]
        {
            WebExceptionStatus.ConnectFailure,   // Unable to connect to the remote server
            WebExceptionStatus.ConnectionClosed, // The underlying connection was closed
            WebExceptionStatus.KeepAliveFailure, // A connection that was expected to be kept alive was closed by the server
            WebExceptionStatus.ReceiveFailure,   // An unexpected error occurred on a receive
        });

        private readonly ILogger<WebExceptionRetryDelegatingHandler> _logger;

        public WebExceptionRetryDelegatingHandler(ILogger<WebExceptionRetryDelegatingHandler> logger)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            try
            {
                return await base.SendAsync(request, cancellationToken);
            }
            catch (Exception ex) when (ex is HttpRequestException hre && hre.InnerException is WebException we)
            {
                if (_transientWebExceptionStatuses.Contains(we.Status))
                {
                    // Retry only a single time since some of these transient exceptions take a while (~20 seconds) to be
                    // thrown and we don't want to make the user wait too long even to see a failure.
                    _logger.LogWarning(ex, "Transient web exception encountered, status {Status}. Attempting a single retry.", we.Status);
                    return await base.SendAsync(request, cancellationToken);
                }
                else
                {
                    _logger.LogError(ex, "Non-transient web exception encountered, status {Status}.", we.Status);
                    throw;
                }
            }
        }
    }
}
