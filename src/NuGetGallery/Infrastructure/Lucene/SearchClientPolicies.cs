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
using Polly.Timeout;

namespace NuGetGallery.Infrastructure.Search
{
    // Implements Polly policies 
    // Samples at https://github.com/App-vNext/Polly-Samples
    // Docs: https://github.com/App-vNext/Polly/wiki/Circuit-Breaker
    public class SearchClientPolicies
    {
        internal const string ContextKey_RequestUri = "RequestUri";
        internal const string ContextKey_CircuitBreakerStatus = "CircuitBreakerStatus";

        /// <summary>
        /// Builds the CircuitBreakerPolicy. Through this policy if <paramref name="breakAfterCount"/> consecutive requests will fail with transient HttpErrors
        /// the circuit breaker will move into Open state. 
        /// It will stay in this state(Open) for the <paramref name="breakDuration"/>. This means that requests made while the circuit is in this state will fail fast.
        /// When the timeout expires trial requsts are allowed. The state will be changed to a Close state if they succeed.
        /// If they do not succeed the circuit will be moved back in the Open state for another <paramref name="breakDuration"/>. 
        /// </summary>
        /// <param name="breakAfterCount">The number of allowed failures until circuit will be open.</param>
        /// <param name="breakDuration">The <see cref="TimeSpan"/> for the circuit to stay open.</param>
        /// <param name="logger">An <see cref="ILogger"/> instance.</param>
        /// <returns>The policy.</returns>
        public static IAsyncPolicy<HttpResponseMessage> SearchClientCircuitBreakerPolicy(int breakAfterCount, TimeSpan breakDuration, ILogger logger, string searchName, ITelemetryService telemetryService)
        {
            // HandleTransientHttpError - handles HttpRequestException or any 500+ and 408(timeout) error codes as below
            // https://github.com/App-vNext/Polly.Extensions.Http/blob/808665304882fb921b1c38cbbd38fcc102229f84/src/Polly.Extensions.Http.Shared/HttpPolicyExtensions.cs
            return HttpPolicyExtensions
                .HandleTransientHttpError()
                .OrResult(r => r.StatusCode == HttpStatusCode.Forbidden)
                .Or<TimeoutRejectedException>()
                .CircuitBreakerAsync(breakAfterCount,
                breakDuration,
                onBreak: (delegateResult, circuitBreakerStatus, breakDelay, context) =>
                {
                    context.Add(ContextKey_CircuitBreakerStatus, circuitBreakerStatus);
                    telemetryService.TrackMetricForSearchCircuitBreakerOnBreak(searchName, delegateResult.Exception, delegateResult.Result, context.CorrelationId.ToString(), GetValueFromContext("RequestUri",context));
                    logger.LogWarning("SearchCircuitBreaker logging: Breaking the circuit for {BreakDelayInMilliseconds} milliseconds due to {Exception} {SearchName}.",
                        breakDelay.TotalMilliseconds,
                        delegateResult.Exception,
                        searchName);
                },
                onReset: (context) =>
                {
                    telemetryService.TrackMetricForSearchCircuitBreakerOnReset(searchName, context.CorrelationId.ToString(), GetValueFromContext("RequestUri", context));
                    logger.LogInformation("SearchCircuitBreaker logging: Call ok! Closed the circuit again! {SearchName}", searchName);
                },
                onHalfOpen: () => logger.LogInformation("SearchCircuitBreaker logging: Half-open: Next call is a trial! {SearchName}", searchName));
        }

        /// <summary>
        /// A WaitAndRetryForever policy to be used with the SearchClientCircuitBreakerPolicy. 
        /// The policy will retry for <paramref name="retryCount"/> on any transient error and will not retry on <see cref="BrokenCircuitException"/> to avoid non-necessary retries due to circuit breaker exceptions.
        /// </summary>
        /// <param name="retryCount">The allowed number of retries.</param>
        /// <param name="waitInMilliseconds">The time to wait between retries.</param>
        /// <param name="logger">The logger</param>
        /// <param name="searchName">The search name.</param>
        /// <param name="telemetryService">Telemetry service</param>
        /// <returns>The policy.</returns>
        public static IAsyncPolicy<HttpResponseMessage> SearchClientWaitAndRetryPolicy(int retryCount, int waitInMilliseconds, ILogger logger, string searchName, ITelemetryService telemetryService)
        {
            return HttpPolicyExtensions
                        .HandleTransientHttpError()
                        .Or<BrokenCircuitException>(
                            (ex) =>
                            {
                                // When the circuit breaker is open a BrokenCircuitException is thrown 
                                // There should not be any retry in this case
                                return false;
                            }).
                        WaitAndRetryAsync(
                            retryCount: retryCount,
                            sleepDurationProvider: (retryAttempt, context) => TimeSpan.FromMilliseconds(waitInMilliseconds),
                            onRetry: (delegateResult, waitDuration, context) =>
                            {
                                telemetryService.TrackMetricForSearchOnRetry(searchName,
                                    delegateResult.Exception,
                                    context.CorrelationId.ToString(),
                                    GetValueFromContext(ContextKey_RequestUri, context),
                                    GetValueFromContext(ContextKey_CircuitBreakerStatus, context));
                                logger.LogInformation("Policy retry - it will retry after {RetryMilliseconds} milliseconds. {Exception} {SearchName}", waitDuration.TotalMilliseconds, delegateResult.Exception, searchName);
                            });
        }

        /// <summary>
        /// In case of exception a <see cref="HttpResponseMessage"/> with status code = 503 is returned.
        /// </summary>
        /// <param name="logger">An <see cref="ILogger"/> instance.</param>
        /// <returns></returns>
        public static IAsyncPolicy<HttpResponseMessage> SearchClientTimeoutPolicy(TimeSpan timeout, ILogger logger, string searchName, ITelemetryService telemetryService)
        {
            return Policy.TimeoutAsync<HttpResponseMessage>(
                timeout,
                onTimeoutAsync: (context, actualTimeout, task) =>
                {
                    telemetryService.TrackMetricForSearchOnTimeout(
                        searchName,
                        context.CorrelationId.ToString(),
                        GetValueFromContext(ContextKey_RequestUri, context),
                        GetValueFromContext(ContextKey_CircuitBreakerStatus, context));

                    logger.LogInformation(
                        "Policy timeout - it will timeout after {Timeout} milliseconds. {SearchName}",
                        actualTimeout,
                        searchName);

                    return Task.CompletedTask;
                });
        }

        /// <summary>
        /// In case of exception a <see cref="HttpResponseMessage"/> with status code = 503 is returned.
        /// </summary>
        /// <param name="logger">An <see cref="ILogger"/> instance.</param>
        /// <returns></returns>
        public static IAsyncPolicy<HttpResponseMessage> SearchClientFallBackCircuitBreakerPolicy(ILogger logger, string searchName, ITelemetryService telemetryService)
        {
            return HttpPolicyExtensions
                        .HandleTransientHttpError()
                        .OrResult(r => r.StatusCode == HttpStatusCode.Forbidden)
                        .Or<Exception>()
                        .FallbackAsync(
                                fallbackAction: (context, cancellationToken) =>
                                {
                                    return Task.FromResult(new HttpResponseMessage()
                                    {
                                        Content = new StringContent(Strings.SearchServiceIsNotAvailable),
                                        StatusCode = HttpStatusCode.ServiceUnavailable
                                    });
                                }, 
                                onFallbackAsync: async (delegateResult, context) =>
                                {
                                    // only to go async
                                    await Task.Yield();
                                    logger.LogInformation("On circuit breaker fallback. {SearchName} {Exception} {Result}", searchName, delegateResult.Exception, delegateResult.Result);
                                });
        }

        private static string GetValueFromContext(string contextKey, Context context)
        {
            object objValue = null;
            return context.TryGetValue(contextKey, out objValue) ? objValue.ToString() : string.Empty;
        }
    }
}