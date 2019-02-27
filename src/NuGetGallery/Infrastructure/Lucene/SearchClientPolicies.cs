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
using NuGetGallery;

namespace NuGetGallery.Infrastructure.Search
{
    // Implements Polly policies 
    // Samples at https://github.com/App-vNext/Polly-Samples
    public class SearchClientPolicies
    {
        /// <summary>
        /// Builds the CircuitBreakerPolicy. Through this policy a request will be retried for <paramref name="breakAfterCount"/> and break after. This will move the circuit breaker into Open state. 
        /// It will stay in this state(Open) for the <paramref name="breakDuration"/>. This means that requests made while the circuit is in this state will fail fast.
        /// When the timeout expires trial requsts are allowed. The state will be changed to a Close state if they succeed.
        /// If they do not succeed the circuit will be moved back in the Open state for another <paramref name="breakDuration"/>. 
        /// </summary>
        /// <param name="breakAfterCount">The number of retries until circuit will be open.</param>
        /// <param name="breakDuration">The <see cref="TimeSpan"/> for the circuit to stay open.</param>
        /// <param name="logger">An <see cref="ILogger"/> instance.</param>
        /// <returns>The policy.</returns>
        public static IAsyncPolicy<HttpResponseMessage> SearchClientCircuitBreakerPolicy(int breakAfterCount, TimeSpan breakDuration, ILogger logger, string searchName, ITelemetryService telemetryService)
        {
            // HandleTransientHttpError - handles HttpRequestException or any 500+ and 408(timeout) error codes as below
            // https://github.com/App-vNext/Polly.Extensions.Http/blob/808665304882fb921b1c38cbbd38fcc102229f84/src/Polly.Extensions.Http.Shared/HttpPolicyExtensions.cs
            return HttpPolicyExtensions
                .HandleTransientHttpError()
                .CircuitBreakerAsync(breakAfterCount,
                breakDuration,
                onBreak: (delegateResult, breakDelay, context) =>
                {
                    telemetryService.TrackMetricForSearchCircuitBreakerOnBreak(searchName, delegateResult.Exception, delegateResult.Result);
                    logger.LogWarning("SearchCircuitBreaker logging: Breaking the circuit for {BreakDelayInMilliseconds} milliseconds due to {Exception} {SearchName}.",
                        breakDelay.TotalMilliseconds,
                        delegateResult.Exception,
                        searchName);
                },
                onReset: (context) =>
                {
                    telemetryService.TrackMetricForSearchCircuitBreakerOnReset(searchName);
                    logger.LogInformation("SearchCircuitBreaker logging: Call ok! Closed the circuit again! {SearchName}", searchName);
                },
                onHalfOpen: () => logger.LogInformation("SearchCircuitBreaker logging: Half-open: Next call is a trial! {SearchName}", searchName));
        }

        /// <summary>
        /// A WaitAndRetryForever policy to be used with the SearchClientCircuitBreakerPolicy. 
        /// The policy will retry on any transient error and will not retry on <see cref="BrokenCircuitException"/> to avoid non-necessary retries due to circuit breaker exceptions.
        /// </summary>
        /// <param name="logger">An <see cref="ILogger"/> instance.</param>
        /// <returns>The policy.</returns>
        public static IAsyncPolicy<HttpResponseMessage> SearchClientWaitAndRetryForeverPolicy(ILogger logger, string searchName, ITelemetryService telemetryService)
        {
            return HttpPolicyExtensions.
                         HandleTransientHttpError()
                        .Or<BrokenCircuitException>(
                            (ex) =>
                            {
                                // When the circuit breaker is open a BrokenCircuitException is thrown 
                                // There should not be any retry in this case
                                return false;
                            }).
                        WaitAndRetryForeverAsync(
                            sleepDurationProvider: retryAttempt => TimeSpan.FromMilliseconds(SearchClientConfiguration.WaitAndRetryDefaultIntervalInMilliseconds), 
                            onRetry: (delegateResult, waitDuration) =>
                                    {
                                        telemetryService.TrackMetricForSearchOnRetry(searchName, delegateResult.Exception);
                                        logger.LogInformation("Policy retry - it will retry after {RetryMilliseconds} milliseconds. {Exception} {SearchName}", waitDuration.TotalMilliseconds, delegateResult.Exception, searchName);
                                    });
        }

        /// <summary>
        /// In case of exception a <see cref="HttpResponseMessage"/> with status code = 503 is returned.
        /// </summary>
        /// <param name="logger">An <see cref="ILogger"/> instance.</param>
        /// <returns></returns>
        public static IAsyncPolicy<HttpResponseMessage> SearchClientFallBackCircuitBreakerPolicy(ILogger logger, string searchName, ITelemetryService telemetryService)
        {
            return HttpPolicyExtensions.
                        HandleTransientHttpError()
                        .Or<Exception>()
                        .FallbackAsync(
                                fallbackAction: async (context, cancellationToken) => {
                                    return await Task.FromResult(new HttpResponseMessage()
                                    {
                                        Content = new StringContent(Strings.SearchServiceIsNotAvailable),
                                        StatusCode = HttpStatusCode.ServiceUnavailable
                                    });}, 
                                onFallbackAsync: async (delegateResult, context) =>
                                {
                                    // only to go async
                                    await Task.Yield();
                                    logger.LogInformation("On circuit breaker fallback. {SearchName} {Exception} {Result}", searchName, delegateResult.Exception, delegateResult.Result);
                                });
        }
    }
}