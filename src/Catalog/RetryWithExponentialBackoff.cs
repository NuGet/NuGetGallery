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
        private readonly HttpCompletionOption _httpCompletionOption;
        private readonly Action<Exception> _onException;

        internal RetryWithExponentialBackoff(
            ushort maximumRetries,
            TimeSpan delay,
            TimeSpan maximumDelay,
            HttpCompletionOption httpCompletionOption,
            Action<Exception> onException)
        {
            _maximumRetries = maximumRetries;
            _delay = delay;
            _maximumDelay = maximumDelay;
            _httpCompletionOption = httpCompletionOption;
            _onException = onException;
        }

        internal RetryWithExponentialBackoff()
        {
            _maximumRetries = 3;
            _delay = TimeSpan.FromSeconds(1);
            _maximumDelay = TimeSpan.FromSeconds(10);
            _httpCompletionOption = HttpCompletionOption.ResponseContentRead;
        }

        public async Task<HttpResponseMessage> SendAsync(HttpClient client, Uri address, CancellationToken cancellationToken)
        {
            var backoff = new ExponentialBackoff(_maximumRetries, _delay, _maximumDelay);

            while (true)
            {
                HttpResponseMessage httpResponse = null;

                try
                {
                    httpResponse = await SendWithForcedTimeoutAsync(client, address, cancellationToken);

                    httpResponse.EnsureSuccessStatusCode();

                    return httpResponse;
                }
                catch (Exception e)
                {
                    _onException?.Invoke(e);
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

        private async Task<HttpResponseMessage> SendWithForcedTimeoutAsync(HttpClient client, Uri address, CancellationToken cancellationToken)
        {
            // Use a forced timeout which is twice the HTTP client's time. This is to allow ample time for the built-in
            // timeout to work.
            var timeout = TimeSpan.FromTicks(client.Timeout.Ticks * 2);

            // Source:
            // https://github.com/davidfowl/AspNetCoreDiagnosticScenarios/blob/master/AsyncGuidance.md#using-a-timeout
            using (var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken))
            {
                var delayTask = Task.Delay(timeout, cts.Token);
                var mainTask = client.GetAsync(address, _httpCompletionOption, cts.Token);
                var resultTask = await Task.WhenAny(mainTask, delayTask);
                if (resultTask == delayTask)
                {
                    if (resultTask.IsCanceled)
                    {
                        // This will throw an OperationCanceledException exception. In this case, the delay task was
                        // canceled by the cancellation token passed into this method.
                        await resultTask;
                    }
                    else
                    {
                        throw new TimeoutException("The operation was forcibly canceled.");
                    }
                }
                else
                {
                    // Cancel the timer task so that it does not fire
                    cts.Cancel();
                }

                return await mainTask;
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
                    response.StatusCode != HttpStatusCode.HttpVersionNotSupported)
                || (response.StatusCode == HttpStatusCode.BadRequest); // We've seen transient HTTP 400s
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