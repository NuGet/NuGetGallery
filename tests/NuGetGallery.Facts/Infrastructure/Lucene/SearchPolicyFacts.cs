// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Moq;
using NuGetGallery.Configuration;
using Xunit;
using Xunit.Abstractions;

namespace NuGetGallery.Infrastructure.Search
{
    public class SearchPolicyFacts
    {
        private int _waitBetweenRetriesInMilliseconds = 500;
        private int _circuitBreakerFailAfter = 2;
        private int _retryCount = 10;
        // set the circuit breaker to be larger than the retryCount
        private int _circuitBreakerFailAfter_2 = 10;
        private int _retryCount_2 = 2;
        private int _circuitBreakerLongDelaySeconds = 600;
        private int _circuitBreakerShortDelaySeconds = 1;
        private int _circuitBreakerTimeoutLongInMilliseconds = 30000;
        private int _circuitBreakerTimeoutShortInMilliseconds = 100;
        private ServiceCollection _services = null;
        private LoggerFor_TestSearchHttpClient _logger;
        private string _nameFor_InvalidTestSearchHttpClientWithLongCircuitBreakDelay = "InvalidTestSearchHttpClientWithLongCircuitBreakDelay";
        private string _nameFor_InvalidTestSearchHttpClientWithShortCircuitBreakDelay = "InvalidTestSearchHttpClientWithShortCircuitBreakDelay";
        private string _nameFor_ValidTestSearchHttpClient = "ValidTestSearchHttpClient";
        private string _nameFor_InvalidTestSearchHttpClientRetryCountExpires = "InvalidTestSearchHttpClientRetryCountExpires";
        private string _nameFor_SearchHttpClientReturning403 = "SearchHttpClientReturning403";
        private string _nameFor_SearchHttpClientTimingOut = "SearchHttpClientTimingOut";
        private readonly string _longInvalidAddress = "https://example-long-invalid";
        private readonly string _shortInvalidAddress = "https://example-short-invalid";
        private readonly string _validAddress = "https://example-valid";
        private readonly string _shortInvalidAddress_2 = "https://example-short-invalid-2";
        private readonly string _403Address = "https://example-403";
        private readonly string _timeoutAddress = "https://example-timeout";

        public SearchPolicyFacts(ITestOutputHelper output)
        {
            _services = ConfigureServices(output);
        }

        [Fact]
        public async Task TestCircuitBreakerForContinuouslyFailingRequests()
        {
            var invalidHttpClient = _services.BuildServiceProvider().GetServices<TestSearchHttpClient>().Single(s => s.BaseAddress == new Uri(_longInvalidAddress));
            var uri = new Uri($"{invalidHttpClient.BaseAddress}query?q=packageid:Newtonsoft.Json version:12.0.1");
            var validUri = new Uri($"{_validAddress}/query?q=packageid:Newtonsoft.Json version:12.0.1");

            var r = await invalidHttpClient.GetAsync(uri);

            var retryInfo = _logger.Informations.Count(s => s.StartsWith("Policy retry - it will retry after"));
            var onCircuitBreakerfallBackInfo = _logger.Informations.Count(s => s.StartsWith("On circuit breaker fallback."));
            var circuitBreakerWarning = _logger.Warnings.Count(s => s.StartsWith("SearchCircuitBreaker logging: Breaking the circuit for"));
            var onCircuitBreakerReset = _logger.Informations.Count(s => s.StartsWith("SearchCircuitBreaker logging: Call ok! Closed the circuit again!"));
            var onCircuitBreakerHalfOpen = _logger.Informations.Count(s => s.StartsWith("SearchCircuitBreaker logging: Half-open: Next call is a trial!"));

            Assert.Equal(2, retryInfo);
            Assert.Equal(1, onCircuitBreakerfallBackInfo);
            Assert.Equal(1, circuitBreakerWarning);
            Assert.Equal(0, onCircuitBreakerReset);
            Assert.Equal(0, onCircuitBreakerHalfOpen);
            Assert.Equal(HttpStatusCode.ServiceUnavailable, r.StatusCode);

            // Request again. The circuit breaker should be open and not allowing requests to pass through
            var r2 = await invalidHttpClient.GetAsync(uri);

            retryInfo = _logger.Informations.Count(s => s.StartsWith("Policy retry - it will retry after"));
            onCircuitBreakerfallBackInfo = _logger.Informations.Count(s => s.StartsWith("On circuit breaker fallback."));
            circuitBreakerWarning = _logger.Warnings.Count(s => s.StartsWith("SearchCircuitBreaker logging: Breaking the circuit for"));

            // No other info added 
            Assert.Equal(2, retryInfo);
            Assert.Equal(2, onCircuitBreakerfallBackInfo);
            Assert.Equal(1, circuitBreakerWarning);
            Assert.Equal(0, onCircuitBreakerReset);
            Assert.Equal(0, onCircuitBreakerHalfOpen);
            Assert.Equal(HttpStatusCode.ServiceUnavailable, r2.StatusCode);

            // Request again with correct uri. The circuit breaker should be open and not allowing requests to pass through
            var r3 = await invalidHttpClient.GetAsync(validUri);

            retryInfo = _logger.Informations.Count(s => s.StartsWith("Policy retry - it will retry after"));
            onCircuitBreakerfallBackInfo = _logger.Informations.Count(s => s.StartsWith("On circuit breaker fallback."));
            circuitBreakerWarning = _logger.Warnings.Count(s => s.StartsWith("SearchCircuitBreaker logging: Breaking the circuit for"));

            // No other info added 
            Assert.Equal(2, retryInfo);
            Assert.Equal(3, onCircuitBreakerfallBackInfo);
            Assert.Equal(1, circuitBreakerWarning);
            Assert.Equal(0, onCircuitBreakerReset);
            Assert.Equal(0, onCircuitBreakerHalfOpen);
            Assert.Equal(HttpStatusCode.ServiceUnavailable, r3.StatusCode);
        }

        [Fact]
        public async Task TestCircuitBreakerForEndpointReturning403()
        {
            var invalidHttpClient = _services.BuildServiceProvider().GetServices<TestSearchHttpClient>().Single(s => s.BaseAddress == new Uri(_403Address));
            var uri = new Uri($"{invalidHttpClient.BaseAddress}query?q=packageid:Newtonsoft.Json version:12.0.1");
            var validUri = new Uri($"{_validAddress}/query?q=packageid:Newtonsoft.Json version:12.0.1");

            var r = await invalidHttpClient.GetAsync(uri);

            var retryInfo = _logger.Informations.Count(s => s.StartsWith("Policy retry - it will retry after"));
            var onCircuitBreakerfallBackInfo = _logger.Informations.Count(s => s.StartsWith("On circuit breaker fallback."));
            var circuitBreakerWarning = _logger.Warnings.Count(s => s.StartsWith("SearchCircuitBreaker logging: Breaking the circuit for"));

            Assert.Equal(0, retryInfo);
            Assert.Equal(1, onCircuitBreakerfallBackInfo);
            Assert.Equal(0, circuitBreakerWarning);
            Assert.Equal(HttpStatusCode.ServiceUnavailable, r.StatusCode);

            // Request again. The circuit breaker should be open and not allowing requests to pass through
            var r2 = await invalidHttpClient.GetAsync(uri);

            retryInfo = _logger.Informations.Count(s => s.StartsWith("Policy retry - it will retry after"));
            onCircuitBreakerfallBackInfo = _logger.Informations.Count(s => s.StartsWith("On circuit breaker fallback."));
            circuitBreakerWarning = _logger.Warnings.Count(s => s.StartsWith("SearchCircuitBreaker logging: Breaking the circuit for"));

            // No other info added 
            Assert.Equal(0, retryInfo);
            Assert.Equal(2, onCircuitBreakerfallBackInfo);
            Assert.Equal(1, circuitBreakerWarning);
            Assert.Equal(HttpStatusCode.ServiceUnavailable, r2.StatusCode);

            // Request again with correct uri. The circuit breaker should be open and not allowing requests to pass through
            var r3 = await invalidHttpClient.GetAsync(validUri);

            retryInfo = _logger.Informations.Count(s => s.StartsWith("Policy retry - it will retry after"));
            onCircuitBreakerfallBackInfo = _logger.Informations.Count(s => s.StartsWith("On circuit breaker fallback."));
            circuitBreakerWarning = _logger.Warnings.Count(s => s.StartsWith("SearchCircuitBreaker logging: Breaking the circuit for"));

            // No other info added 
            Assert.Equal(0, retryInfo);
            Assert.Equal(3, onCircuitBreakerfallBackInfo);
            Assert.Equal(1, circuitBreakerWarning);
            Assert.Equal(HttpStatusCode.ServiceUnavailable, r3.StatusCode);
        }

        [Fact]
        public async Task TestCircuitBreakerForEndpointTimingOut()
        {
            var invalidHttpClient = _services.BuildServiceProvider().GetServices<TestSearchHttpClient>().Single(s => s.BaseAddress == new Uri(_timeoutAddress));
            var uri = new Uri($"{invalidHttpClient.BaseAddress}query?q=packageid:Newtonsoft.Json version:12.0.1");
            var validUri = new Uri($"{_validAddress}/query?q=packageid:Newtonsoft.Json version:12.0.1");

            var r = await invalidHttpClient.GetAsync(uri);

            var timeoutInfo = _logger.Informations.Count(s => s.StartsWith("Policy timeout - it will timeout after"));
            var retryInfo = _logger.Informations.Count(s => s.StartsWith("Policy retry - it will retry after"));
            var onCircuitBreakerfallBackInfo = _logger.Informations.Count(s => s.StartsWith("On circuit breaker fallback."));
            var circuitBreakerWarning = _logger.Warnings.Count(s => s.StartsWith("SearchCircuitBreaker logging: Breaking the circuit for"));

            Assert.Equal(1, timeoutInfo);
            Assert.Equal(0, retryInfo);
            Assert.Equal(1, onCircuitBreakerfallBackInfo);
            Assert.Equal(0, circuitBreakerWarning);
            Assert.Equal(HttpStatusCode.ServiceUnavailable, r.StatusCode);

            // Request again. The circuit breaker should be open and not allowing requests to pass through
            var r2 = await invalidHttpClient.GetAsync(uri);

            timeoutInfo = _logger.Informations.Count(s => s.StartsWith("Policy timeout - it will timeout after"));
            retryInfo = _logger.Informations.Count(s => s.StartsWith("Policy retry - it will retry after"));
            onCircuitBreakerfallBackInfo = _logger.Informations.Count(s => s.StartsWith("On circuit breaker fallback."));
            circuitBreakerWarning = _logger.Warnings.Count(s => s.StartsWith("SearchCircuitBreaker logging: Breaking the circuit for"));

            Assert.Equal(2, timeoutInfo);
            Assert.Equal(0, retryInfo);
            Assert.Equal(2, onCircuitBreakerfallBackInfo);
            Assert.Equal(1, circuitBreakerWarning);
            Assert.Equal(HttpStatusCode.ServiceUnavailable, r2.StatusCode);

            // Request again with correct uri. The circuit breaker should be open and not allowing requests to pass through
            var r3 = await invalidHttpClient.GetAsync(validUri);

            timeoutInfo = _logger.Informations.Count(s => s.StartsWith("Policy timeout - it will timeout after"));
            retryInfo = _logger.Informations.Count(s => s.StartsWith("Policy retry - it will retry after"));
            onCircuitBreakerfallBackInfo = _logger.Informations.Count(s => s.StartsWith("On circuit breaker fallback."));
            circuitBreakerWarning = _logger.Warnings.Count(s => s.StartsWith("SearchCircuitBreaker logging: Breaking the circuit for"));

            Assert.Equal(2, timeoutInfo);
            Assert.Equal(0, retryInfo);
            Assert.Equal(3, onCircuitBreakerfallBackInfo);
            Assert.Equal(1, circuitBreakerWarning);
            Assert.Equal(HttpStatusCode.ServiceUnavailable, r3.StatusCode);
        }

        [Fact]
        public async Task TestCircuitBreakerForRecoveringRequests()
        {
            var invalidHttpClient = _services.BuildServiceProvider().GetServices<TestSearchHttpClient>().Single(s => s.BaseAddress == new Uri(_shortInvalidAddress));
            var uri = new Uri($"{invalidHttpClient.BaseAddress}query?q=packageid:Newtonsoft.Json version:12.0.1");
            var validUri = new Uri($"{_validAddress}/query?q=packageid:Newtonsoft.Json version:12.0.1");

            var r = await invalidHttpClient.GetAsync(uri);

            var retryInfo = _logger.Informations.Count(s => s.StartsWith("Policy retry - it will retry after"));
            var onCircuitBreakerfallBackInfo = _logger.Informations.Count(s => s.StartsWith("On circuit breaker fallback."));
            var circuitBreakerWarning = _logger.Warnings.Count(s => s.StartsWith("SearchCircuitBreaker logging: Breaking the circuit for"));
            var onCircuitBreakerReset = _logger.Informations.Count(s => s.StartsWith("SearchCircuitBreaker logging: Call ok! Closed the circuit again!"));
            var onCircuitBreakerHalfOpen = _logger.Informations.Count(s => s.StartsWith("SearchCircuitBreaker logging: Half-open: Next call is a trial!"));

            Assert.Equal(2, retryInfo);
            Assert.Equal(1, onCircuitBreakerfallBackInfo);
            Assert.Equal(1, circuitBreakerWarning);
            Assert.Equal(0, onCircuitBreakerReset);
            Assert.Equal(0, onCircuitBreakerHalfOpen);
            Assert.Equal(HttpStatusCode.ServiceUnavailable, r.StatusCode);

            Task.Delay(TimeSpan.FromSeconds(_circuitBreakerShortDelaySeconds)).Wait();

            // Request again with correct uri. The circuit breaker delay should be expired and will do the trial and pass 
            var r2 = await invalidHttpClient.GetAsync(validUri);

            retryInfo = _logger.Informations.Count(s => s.StartsWith("Policy retry - it will retry after"));
            onCircuitBreakerfallBackInfo = _logger.Informations.Count(s => s.StartsWith("On circuit breaker fallback."));
            circuitBreakerWarning = _logger.Warnings.Count(s => s.StartsWith("SearchCircuitBreaker logging: Breaking the circuit for"));
            onCircuitBreakerReset = _logger.Informations.Count(s => s.StartsWith("SearchCircuitBreaker logging: Call ok! Closed the circuit again!"));
            onCircuitBreakerHalfOpen = _logger.Informations.Count(s => s.StartsWith("SearchCircuitBreaker logging: Half-open: Next call is a trial!"));

            // No other info added 
            Assert.Equal(2, retryInfo);
            Assert.Equal(1, onCircuitBreakerfallBackInfo);
            Assert.Equal(1, circuitBreakerWarning);
            Assert.Equal(1, onCircuitBreakerReset);
            Assert.Equal(1, onCircuitBreakerHalfOpen);
            Assert.Equal(HttpStatusCode.OK, r2.StatusCode);
        }

        [Fact]
        public async Task TestCircuitBreakerForValidRequests()
        {
            var validClient = _services.BuildServiceProvider().GetServices<TestSearchHttpClient>().Single(s => s.BaseAddress == new Uri(_validAddress));
            var uri = new Uri($"{validClient.BaseAddress}query?q=packageid:Newtonsoft.Json version:12.0.1");

            var r = await validClient.GetAsync(uri);

            var retryInfo = _logger.Informations.Count(s => s.StartsWith("Policy retry - it will retry after"));
            var onCircuitBreakerfallBackInfo = _logger.Informations.Count(s => s.StartsWith("On circuit breaker fallback."));
            var circuitBreakerWarning = _logger.Warnings.Count(s => s.StartsWith("SearchCircuitBreaker logging: Breaking the circuit for"));
            var onCircuitBreakerReset = _logger.Informations.Count(s => s.StartsWith("SearchCircuitBreaker logging: Call ok! Closed the circuit again!"));
            var onCircuitBreakerHalfOpen = _logger.Informations.Count(s => s.StartsWith("SearchCircuitBreaker logging: Half-open: Next call is a trial!"));

            Assert.Equal(0, retryInfo);
            Assert.Equal(0, onCircuitBreakerfallBackInfo);
            Assert.Equal(0, circuitBreakerWarning);
            Assert.Equal(0, onCircuitBreakerReset);
            Assert.Equal(0, onCircuitBreakerHalfOpen);
            Assert.Equal(HttpStatusCode.OK, r.StatusCode);
        }

        [Fact]
        public async Task TestWaitAndRetryForInvalidRequests()
        {
            var invalidClient = _services.BuildServiceProvider().GetServices<TestSearchHttpClient>().Where(s => s.BaseAddress == new Uri(_shortInvalidAddress_2)).ElementAt(0);
            var uri = new Uri($"{invalidClient.BaseAddress}query?q=packageid:Newtonsoft.Json version:12.0.1");

            var r = await invalidClient.GetAsync(uri);

            var retryInfo = _logger.Informations.Count(s => s.StartsWith("Policy retry - it will retry after"));
            var onCircuitBreakerfallBackInfo = _logger.Informations.Count(s => s.StartsWith("On circuit breaker fallback."));
            var circuitBreakerWarning = _logger.Warnings.Count(s => s.StartsWith("SearchCircuitBreaker logging: Breaking the circuit for"));
            var onCircuitBreakerReset = _logger.Informations.Count(s => s.StartsWith("SearchCircuitBreaker logging: Call ok! Closed the circuit again!"));
            var onCircuitBreakerHalfOpen = _logger.Informations.Count(s => s.StartsWith("SearchCircuitBreaker logging: Half-open: Next call is a trial!"));

            Assert.Equal(_retryCount_2, retryInfo);
            Assert.Equal(1, onCircuitBreakerfallBackInfo);
            Assert.Equal(0, circuitBreakerWarning);
            Assert.Equal(0, onCircuitBreakerReset);
            Assert.Equal(0, onCircuitBreakerHalfOpen);
            Assert.Equal(HttpStatusCode.ServiceUnavailable, r.StatusCode);
        }

        private TestHttpHandler MakeTestHttpHandler(ILogger logger)
        {
            var handler = new TestHttpHandler(logger);

            handler.Handlers[_longInvalidAddress] = (r, ct) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.ServiceUnavailable));
            handler.Handlers[_shortInvalidAddress] = (r, ct) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.ServiceUnavailable));
            handler.Handlers[_shortInvalidAddress_2] = (r, ct) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.ServiceUnavailable));
            handler.Handlers[_validAddress] = (r, ct) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK));
            handler.Handlers[_403Address] = (r, ct) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.Forbidden));
            handler.Handlers[_timeoutAddress] = async (r, ct) =>
            {
                await Task.Delay(_circuitBreakerTimeoutShortInMilliseconds + 30000, ct);
                return new HttpResponseMessage(HttpStatusCode.OK);
            };

            return handler;
        }

        private ServiceCollection ConfigureServices(ITestOutputHelper output)
        {
            Mock<ITelemetryService> mockITelemetryService = new Mock<ITelemetryService>();
            ITelemetryService telemetryServiceResolver = mockITelemetryService.Object;

            ServiceCollection services = new ServiceCollection();

            _logger = new LoggerFor_TestSearchHttpClient(output);
            services.AddSingleton<ILogger>(_logger);

            AddTestHttpClient(
                services,
                _longInvalidAddress,
                _nameFor_InvalidTestSearchHttpClientWithLongCircuitBreakDelay,
                _retryCount,
                _waitBetweenRetriesInMilliseconds,
                _circuitBreakerTimeoutLongInMilliseconds,
                _circuitBreakerFailAfter,
                _circuitBreakerShortDelaySeconds,
                telemetryServiceResolver);

            AddTestHttpClient(
                services,
                _shortInvalidAddress,
                _nameFor_InvalidTestSearchHttpClientWithShortCircuitBreakDelay,
                _retryCount,
                _waitBetweenRetriesInMilliseconds,
                _circuitBreakerTimeoutLongInMilliseconds,
                _circuitBreakerFailAfter,
                _circuitBreakerShortDelaySeconds,
                telemetryServiceResolver);

            AddTestHttpClient(
                services,
                _validAddress,
                _nameFor_ValidTestSearchHttpClient,
                _retryCount,
                _waitBetweenRetriesInMilliseconds,
                _circuitBreakerTimeoutLongInMilliseconds,
                _circuitBreakerFailAfter,
                _circuitBreakerShortDelaySeconds,
                telemetryServiceResolver);

            AddTestHttpClient(
                services,
                _shortInvalidAddress_2,
                _nameFor_InvalidTestSearchHttpClientRetryCountExpires,
                _retryCount_2,
                _waitBetweenRetriesInMilliseconds,
                _circuitBreakerTimeoutLongInMilliseconds,
                _circuitBreakerFailAfter_2,
                _circuitBreakerShortDelaySeconds,
                telemetryServiceResolver);

            AddTestHttpClient(
                services,
                _403Address,
                _nameFor_SearchHttpClientReturning403,
                _retryCount,
                _waitBetweenRetriesInMilliseconds,
                _circuitBreakerTimeoutLongInMilliseconds,
                _circuitBreakerFailAfter,
                _circuitBreakerLongDelaySeconds,
                telemetryServiceResolver);

            AddTestHttpClient(
                services,
                _timeoutAddress,
                _nameFor_SearchHttpClientTimingOut,
                _retryCount,
                _waitBetweenRetriesInMilliseconds,
                _circuitBreakerTimeoutShortInMilliseconds,
                _circuitBreakerFailAfter,
                _circuitBreakerShortDelaySeconds,
                telemetryServiceResolver);

            return services;
        }

        private void AddTestHttpClient(
            ServiceCollection services,
            string baseAddress,
            string name,
            int retryCount,
            int waitInMilliseconds,
            int timeoutInMilliseconds,
            int failAfter,
            int circuitBreakerDelaySeconds,
            ITelemetryService telemetryService)
        {
            var config = new AppConfiguration
            {
                SearchCircuitBreakerWaitAndRetryCount = retryCount,
                SearchCircuitBreakerWaitAndRetryIntervalInMilliseconds = waitInMilliseconds,
                SearchCircuitBreakerBreakAfterCount = failAfter,
                SearchCircuitBreakerDelayInSeconds = circuitBreakerDelaySeconds,
                SearchHttpRequestTimeoutInMilliseconds = timeoutInMilliseconds,
            };

            services.AddHttpClient<TestSearchHttpClient>(name, c => c.BaseAddress = new Uri(baseAddress))
                    .AddSearchPolicyHandlers(_logger, name, telemetryService, config)
                    .AddHttpMessageHandler(() => MakeTestHttpHandler(_logger));
        }

        private class TestHttpHandler : DelegatingHandler
        {
            private readonly ILogger _logger;

            public Dictionary<string, Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>>> Handlers { get; } =
                new Dictionary<string, Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>>>();

            public TestHttpHandler(ILogger logger)
            {
                _logger = logger;
            }

            protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            {
                _logger.LogInformation(
                    "[{Type}] {Method} {RequestUri}",
                    nameof(TestHttpHandler),
                    request.Method,
                    request.RequestUri.AbsoluteUri);

                var schemeAndServer = request.RequestUri.GetComponents(UriComponents.SchemeAndServer, UriFormat.Unescaped);

                try
                {
                    if (Handlers.TryGetValue(schemeAndServer, out var handlerAsync))
                    {
                        var response = await handlerAsync(request, cancellationToken);
                        _logger.LogInformation(
                            "[{Type}] {StatusCode} {ReasonPhrase}",
                            nameof(TestHttpHandler),
                            (int)response.StatusCode,
                            response.ReasonPhrase);
                        return response;
                    }

                    throw new NotImplementedException($"Scheme and server '{schemeAndServer}' does not have a handler.");
                }
                catch (Exception ex)
                {
                    _logger.LogError(0, ex, "[{Type}] EXCEPTION", nameof(TestHttpHandler));
                    throw;
                }
            }
        }

        private class TestSearchHttpClient : IHttpClientWrapper
        {
            private readonly HttpClient _client;
            private readonly ILogger _logger;

            public TestSearchHttpClient(HttpClient client, ILogger logger)
            {
                _client = client;
                _logger = logger;
            }

            public Uri BaseAddress => _client.BaseAddress;

            public async Task<HttpResponseMessage> GetAsync(Uri uri)
            {
                _logger.LogInformation("[{Type}] GET {RequestUri}", nameof(TestSearchHttpClient), uri.AbsoluteUri);
                HttpResponseMessage m = null;
                try
                {
                    m = await _client.GetAsync(uri);
                    _logger.LogInformation(
                        "[{Type}] {(StatusCode} {ReasonPhrase}",
                        nameof(TestSearchHttpClient),
                        (int)m.StatusCode,
                        m.ReasonPhrase);
                    return m;
                }
                catch (Exception ex)
                {
                    _logger.LogError(0, ex, "[{Type}] EXCEPTION", nameof(TestSearchHttpClient));
                    throw;
                }
            }
        }

        private class LoggerFor_TestSearchHttpClient : ILogger
        {
            private readonly ITestOutputHelper _output;
            public List<string> Warnings = new List<string>();
            public List<string> Informations = new List<string>();

            public LoggerFor_TestSearchHttpClient(ITestOutputHelper output)
            {
                _output = output;
            }

            public IDisposable BeginScope<TState>(TState state)
            {
                throw new NotImplementedException();
            }

            public bool IsEnabled(LogLevel logLevel)
            {
                return true;
            }

            public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception exception, Func<TState, Exception, string> formatter)
            {
                var output = $"[{logLevel}] {formatter(state, exception)}";
                if (exception != null)
                {
                    output += $" {exception.GetType().FullName}: {exception.Message}";
                }

                _output.WriteLine(output);

                switch (logLevel)
                {
                    case LogLevel.Warning:
                        Warnings.Add(state.ToString());
                        break;
                    case LogLevel.Information:
                        Informations.Add(state.ToString());
                        break;
                    default:
                        break;
                }
            }
        }
    }
}
