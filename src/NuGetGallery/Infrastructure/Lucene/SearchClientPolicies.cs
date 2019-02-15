// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Polly;
using Polly.CircuitBreaker;
using Polly.Extensions.Http;
using NuGet.Services.Search.Client;

namespace NuGetGallery.Infrastructure.Lucene
{
    // Implements Polly policies 
    // Samples at https://github.com/App-vNext/Polly-Samples
    public class SearchClientPolicies
    {
        /// <summary>
        /// Builds the CircuitBreakerPolicy
        /// </summary>
        /// <param name="breakAfterCount">The number of retries until circuit will be open.</param>
        /// <param name="breakDuration">The <see cref="TimeSpan"/> for the circuit to stay open.</param>
        /// <param name="logger">An <see cref="ILogger"/> instance.</param>
        /// <returns>The policy.</returns>
        public static IAsyncPolicy<HttpResponseMessage> SearchClientCircuitBreakerPolicy(int breakAfterCount, TimeSpan breakDuration, ILogger logger)
        {
            // HandleTransientHttpError - handles HttpRequestException or any 500+ and 408(timeout) error codes as below
            // https://github.com/App-vNext/Polly.Extensions.Http/blob/808665304882fb921b1c38cbbd38fcc102229f84/src/Polly.Extensions.Http.Shared/HttpPolicyExtensions.cs
            return HttpPolicyExtensions
                .HandleTransientHttpError()
                .CircuitBreakerAsync(breakAfterCount,
                breakDuration,
                onBreak: (ex, breakDelay, context) =>
                {
                    
                    logger.LogWarning("SearchCircuitBreaker logging: Breaking the circuit for {BreakDelayInMilliseconds} milliseconds due to {Exception}.",
                        breakDelay.TotalMilliseconds,
                        ex);
                },
                onReset: (context) => logger.LogInformation("SearchCircuitBreaker logging: Call ok! Closed the circuit again!"),
                onHalfOpen: () => logger.LogInformation("SearchCircuitBreaker logging: Half-open: Next call is a trial!"));
        }

        /// <summary>
        /// A WaitAndRetryForever policy to be used with the SearchClientCircuitBreakerPolicy. 
        /// The policy will retry on any transient error and will not retry on <see cref="BrokenCircuitException"/> to avoid non-necessary retries due to circuit breaker exceptions.
        /// </summary>
        /// <param name="logger">An <see cref="ILogger"/> instance.</param>
        /// <returns>The policy.</returns>
        public static IAsyncPolicy<HttpResponseMessage> SearchClientWaitAndRetryForeverPolicy(ILogger logger)
        {
            return HttpPolicyExtensions.
                        HandleTransientHttpError().
                        Or<BrokenCircuitException>(
                            (ex) =>
                            {
                                // Do not retry on CircuitBreakerException
                                return false;
                            }).
                        WaitAndRetryForeverAsync(
                            sleepDurationProvider: retryAttempt => TimeSpan.FromMilliseconds(SearchClientConfiguration.WaitAndRetryDefaultIntervalInMilliseconds), 
                            onRetry: (responseMessage, waitDuration) =>
                                    {
                                        logger.LogInformation("Policy retry - it will retry after {RetryMilliseconds} milliseconds. {Exception}", waitDuration.TotalMilliseconds, responseMessage.Exception);
                                    });
        }

        /// <summary>
        /// In case of exception a <see cref="HttpResponseMessage"/> with status code = 503 is returned.
        /// </summary>
        /// <param name="logger">An <see cref="ILogger"/> instance.</param>
        /// <returns></returns>
        public static IAsyncPolicy<HttpResponseMessage> SearchClientFallBackCircuitBreakerPolicy(ILogger logger)
        {
            return HttpPolicyExtensions.
                        HandleTransientHttpError().
                        Or<Exception>().
                        FallbackAsync(
                                fallbackAction: async (context, cancellationToken) => {
                                    return await Task.FromResult(new HttpResponseMessage()
                                    {
                                        Content = new StringContent(Strings.SearchServiceIsNotAvailable),
                                        StatusCode = HttpStatusCode.ServiceUnavailable
                                    });}, 
                                onFallbackAsync: async (e, context) =>
                                {
                                    // only to go async
                                    await Task.Yield();
                                    logger.LogInformation("On circuit breaker fallback. {Exception} {Result}", e.Exception, e.Result);
                                });
        }
    }
}