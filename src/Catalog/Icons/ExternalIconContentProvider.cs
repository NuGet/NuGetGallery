// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace NuGet.Services.Metadata.Catalog.Icons
{
    public class ExternalIconContentProvider : IExternalIconContentProvider
    {
        private readonly IHttpClient _httpResponseMessageProvider;
        private readonly ILogger<ExternalIconContentProvider> _logger;

        public ExternalIconContentProvider(
            IHttpClient httpResponseMessageProvider,
            ILogger<ExternalIconContentProvider> logger)
        {
            _httpResponseMessageProvider = httpResponseMessageProvider ?? throw new ArgumentNullException(nameof(httpResponseMessageProvider));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public async Task<TryGetResponseResult> TryGetResponseAsync(Uri iconUrl, CancellationToken cancellationToken)
        {
            try
            {
                return TryGetResponseResult.Success(await _httpResponseMessageProvider.GetAsync(iconUrl, cancellationToken));
            }
            catch (HttpRequestException e) when (IsConnectFailure(e))
            {
                _logger.LogInformation("Failed to connect to remote host to retrieve the icon");
            }
            catch (HttpRequestException e) when (IsDnsFailure(e))
            {
                _logger.LogInformation("Failed to resolve DNS name for the icon URL");
                return TryGetResponseResult.FailCannotRetry();
            }
            catch (HttpRequestException e) when (IsConnectionClosed(e))
            {
                _logger.LogInformation("Connection closed unexpectedly while trying to retrieve the icon");
            }
            catch (HttpRequestException e) when (IsTLSSetupFailure(e))
            {
                _logger.LogInformation("TLS setup failed while trying to retrieve the icon");
            }
            catch (TaskCanceledException e) when (e.CancellationToken != cancellationToken)
            {
                _logger.LogInformation("Timed out while trying to get the icon data");
            }
            catch (HttpRequestException e)
            {
                _logger.LogError(0, e, "HTTP exception while trying to retrieve icon file");
            }
            catch (Exception e)
            {
                _logger.LogError(0, e, "Exception while trying to retrieve URL: {IconUrl}", iconUrl);
            }
            return TryGetResponseResult.FailCanRetry();
        }

        private static bool IsConnectFailure(HttpRequestException e)
        {
            return (e?.InnerException as WebException)?.Status == WebExceptionStatus.ConnectFailure;
        }

        private static bool IsDnsFailure(HttpRequestException e)
        {
            return (e?.InnerException as WebException)?.Status == WebExceptionStatus.NameResolutionFailure;
        }

        private static bool IsConnectionClosed(HttpRequestException e)
        {
            return (e?.InnerException as WebException)?.Status == WebExceptionStatus.ConnectionClosed;
        }

        private static bool IsTLSSetupFailure(HttpRequestException e)
        {
            var innerWebException = e?.InnerException as WebException;
            return innerWebException?.Status == WebExceptionStatus.TrustFailure || innerWebException?.Status == WebExceptionStatus.SecureChannelFailure;
        }
    }
}
