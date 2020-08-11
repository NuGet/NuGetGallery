// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace NuGet.Services.Metadata.Catalog
{
    // See https://docs.microsoft.com/en-us/dotnet/standard/microservices-architecture/implement-resilient-applications/implement-custom-http-call-retries-exponential-backoff
    public sealed class RetryWithExponentialBackoff : IHttpRetryStrategy
    {
        private readonly ushort _maximumRetries;
        private readonly TimeSpan _delay;
        private readonly TimeSpan _maximumDelay;

        internal RetryWithExponentialBackoff()
        {
            _maximumRetries = 3;
            _delay = TimeSpan.FromSeconds(1);
            _maximumDelay = TimeSpan.FromSeconds(10);
        }

        public async Task<HttpResponseMessage> SendAsync(HttpClient client, Uri address, CancellationToken cancellationToken)
        {
            var backoff = new ExponentialBackoff(_maximumRetries, _delay, _maximumDelay);

            while (true)
            {
                HttpResponseMessage httpResponse = null;

                try
                {
                    httpResponse = await client.GetAsync(address, cancellationToken);

                    httpResponse.EnsureSuccessStatusCode();

                    return httpResponse;
                }
                catch (Exception e)
                {
                    httpResponse?.Dispose();
                    if (IsTransientError(e, httpResponse))
                    {
                        await backoff.Delay();
                    }
                    else
                    {
                        throw;
                    }
                }
            }
        }

        public static bool IsTransientError(Exception e, HttpResponseMessage response)
        {
            if (!(e is HttpRequestException || e is OperationCanceledException))
            {
                return false;
            }

            return response == null
                || ((int)response.StatusCode >= 500 &&
                    response.StatusCode != HttpStatusCode.NotImplemented &&
                    response.StatusCode != HttpStatusCode.HttpVersionNotSupported);
        }

        private sealed class ExponentialBackoff
        {
            private readonly ushort _maximumRetries;
            private readonly TimeSpan _delay;
            private readonly TimeSpan _maximumDelay;
            private ushort _retries;
            private int _power;

            internal ExponentialBackoff(ushort maximumRetries, TimeSpan delay, TimeSpan maximumDelay)
            {
                _maximumRetries = maximumRetries;
                _delay = delay;
                _maximumDelay = maximumDelay;
                _retries = 0;
                _power = 1;
            }

            internal Task Delay()
            {
                if (_retries == _maximumRetries)
                {
                    throw new TimeoutException("Maximum retry attempts exhausted.");
                }

                ++_retries;

                if (_retries < 31)
                {
                    _power = _power << 1;
                }

                int delay = (int)Math.Min(_delay.TotalMilliseconds * (_power - 1) / 2, _maximumDelay.TotalMilliseconds);

                return Task.Delay(delay);
            }
        }
    }
}