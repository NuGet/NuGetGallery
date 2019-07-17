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
using Xunit;

namespace NuGetGallery.Infrastructure.Search
{
    public class SearchPolicyFacts
    {
        private static int _waitBetweenRetriesInMilliseconds = 500;
        private static int _circuitBreakerFailAfter = 2;
        private static int _retryCount = 10;
        // set the circuit breker to be larger than the retryCount
        private static int _circuitBreakerFailAfter_2 = 10;
        private static int _retryCount_2 = 2;
        private static int _circuitBreakerLongDelaySeconds = 600;
        private static int _circuitBreakerShortDelaySeconds = 1;
        private static ServiceCollection _services = null;
        private static readonly object _lockServices = new object();
        private static LoggerFor_TestSearchHttpClient _loggerFor_InvalidTestSearchHttpClientWithLongCircuitBreakDelay;
        private static LoggerFor_TestSearchHttpClient _loggerFor_InvalidTestSearchHttpClientWithShortCircuitBreakDelay;
        private static LoggerFor_TestSearchHttpClient _loggerFor_ValidTestSearchHttpClient;
        private static LoggerFor_TestSearchHttpClient _loggerFor_InvalidTestSearchHttpClientRetryCountExpires;
        private static LoggerFor_TestSearchHttpClient _loggerFor_SearchHttpClientReturning403;
        private static string _nameFor_InvalidTestSearchHttpClientWithLongCircuitBreakDelay = "InvalidTestSearchHttpClientWithLongCircuitBreakDelay";
        private static string _nameFor_InvalidTestSearchHttpClientWithShortCircuitBreakDelay = "InvalidTestSearchHttpClientWithShortCircuitBreakDelay";
        private static string _nameFor_ValidTestSearchHttpClient = "ValidTestSearchHttpClient";
        private static string _nameFor_InvalidTestSearchHttpClientRetryCountExpires = "InvalidTestSearchHttpClientRetryCountExpires";
        private static string _nameFor_SearchHttpClientReturning403 = "SearchHttpClientReturning403";
        private static readonly string _longInvalidAddress = "https://example-long-invalid";
        private static readonly string _shortInvalidAddress = "https://example-short-invalid";
        private static readonly string _validAddress = "https://example-valid";
        private static readonly string _shortInvalidAddress_2 = "https://example-short-invalid-2";
        private static readonly string _403Address = "https://example-403";

        public SearchPolicyFacts()
        {
            if(_services == null)
            {
                lock(_lockServices)
                {
                    if(_services == null)
                    {
                        _services = ConfigureServices();
                    }
                }
            }
        }

        [Fact]
        public async Task TestCircuitBreakerForContinuouslyFailingRequests()
        {
            var invalidHttpClient = _services.BuildServiceProvider().GetServices<TestSearchHttpClient>().Where(s => s.BaseAddress == new Uri(_longInvalidAddress)).ElementAt(0);
            var uri = new Uri($"{invalidHttpClient.BaseAddress}query?q=packageid:Newtonsoft.Json version:12.0.1");
            var validUri = new Uri($"{_validAddress}/query?q=packageid:Newtonsoft.Json version:12.0.1");

            var r = await invalidHttpClient.GetAsync(uri);

            var retryInfo = _loggerFor_InvalidTestSearchHttpClientWithLongCircuitBreakDelay.Informations.Where(s => s.StartsWith("Policy retry - it will retry after")).Count();
            var onCircuitBreakerfallBackInfo = _loggerFor_InvalidTestSearchHttpClientWithLongCircuitBreakDelay.Informations.Where(s => s.StartsWith("On circuit breaker fallback.")).Count();
            var circuitBreakerWarning = _loggerFor_InvalidTestSearchHttpClientWithLongCircuitBreakDelay.Warnings.Where(s => s.StartsWith("SearchCircuitBreaker logging: Breaking the circuit for")).Count();
            var onCircuitBreakerReset = _loggerFor_InvalidTestSearchHttpClientWithLongCircuitBreakDelay.Informations.Where(s => s.StartsWith("SearchCircuitBreaker logging: Call ok! Closed the circuit again!")).Count();
            var onCircuitBreakerHalfOpen = _loggerFor_InvalidTestSearchHttpClientWithLongCircuitBreakDelay.Informations.Where(s => s.StartsWith("SearchCircuitBreaker logging: Half-open: Next call is a trial!")).Count();

            Assert.Equal(2, retryInfo);
            Assert.Equal(1, onCircuitBreakerfallBackInfo);
            Assert.Equal(1, circuitBreakerWarning);
            Assert.Equal(0, onCircuitBreakerReset);
            Assert.Equal(0, onCircuitBreakerHalfOpen);
            Assert.Equal(HttpStatusCode.ServiceUnavailable, r.StatusCode);

            // Request again. The circuit breaker should be open and not allowing requests to pass through
            var r2 = await invalidHttpClient.GetAsync(uri);

            retryInfo = _loggerFor_InvalidTestSearchHttpClientWithLongCircuitBreakDelay.Informations.Where(s => s.StartsWith("Policy retry - it will retry after")).Count();
            onCircuitBreakerfallBackInfo = _loggerFor_InvalidTestSearchHttpClientWithLongCircuitBreakDelay.Informations.Where(s => s.StartsWith("On circuit breaker fallback.")).Count();
            circuitBreakerWarning = _loggerFor_InvalidTestSearchHttpClientWithLongCircuitBreakDelay.Warnings.Where(s => s.StartsWith("SearchCircuitBreaker logging: Breaking the circuit for")).Count();

            // No other info added 
            Assert.Equal(2, retryInfo);
            Assert.Equal(2, onCircuitBreakerfallBackInfo);
            Assert.Equal(1, circuitBreakerWarning);
            Assert.Equal(0, onCircuitBreakerReset);
            Assert.Equal(0, onCircuitBreakerHalfOpen);
            Assert.Equal(HttpStatusCode.ServiceUnavailable, r2.StatusCode);

            // Request again with correct uri. The circuit breaker should be open and not allowing requests to pass through
            var r3 = await invalidHttpClient.GetAsync(validUri);

            retryInfo = _loggerFor_InvalidTestSearchHttpClientWithLongCircuitBreakDelay.Informations.Where(s => s.StartsWith("Policy retry - it will retry after")).Count();
            onCircuitBreakerfallBackInfo = _loggerFor_InvalidTestSearchHttpClientWithLongCircuitBreakDelay.Informations.Where(s => s.StartsWith("On circuit breaker fallback.")).Count();
            circuitBreakerWarning = _loggerFor_InvalidTestSearchHttpClientWithLongCircuitBreakDelay.Warnings.Where(s => s.StartsWith("SearchCircuitBreaker logging: Breaking the circuit for")).Count();

            // No other info added 
            Assert.Equal(2, retryInfo);
            Assert.Equal(3, onCircuitBreakerfallBackInfo);
            Assert.Equal(1, circuitBreakerWarning);
            Assert.Equal(0, onCircuitBreakerReset);
            Assert.Equal(0, onCircuitBreakerHalfOpen);
            Assert.Equal(HttpStatusCode.ServiceUnavailable, r2.StatusCode);
        }

        [Fact]
        public async Task TestCircuitBreakerForEndpointReturning403()
        {
            var invalidHttpClient = _services.BuildServiceProvider().GetServices<TestSearchHttpClient>().Where(s => s.BaseAddress == new Uri(_403Address)).ElementAt(0);
            var uri = new Uri($"{invalidHttpClient.BaseAddress}query?q=packageid:Newtonsoft.Json version:12.0.1");
            var validUri = new Uri($"{_validAddress}/query?q=packageid:Newtonsoft.Json version:12.0.1");

            var r = await invalidHttpClient.GetAsync(uri);

            var retryInfo = _loggerFor_SearchHttpClientReturning403.Informations.Where(s => s.StartsWith("Policy retry - it will retry after")).Count();
            var onCircuitBreakerfallBackInfo = _loggerFor_SearchHttpClientReturning403.Informations.Where(s => s.StartsWith("On circuit breaker fallback.")).Count();
            var circuitBreakerWarning = _loggerFor_SearchHttpClientReturning403.Warnings.Where(s => s.StartsWith("SearchCircuitBreaker logging: Breaking the circuit for")).Count();

            Assert.Equal(0, retryInfo);
            Assert.Equal(1, onCircuitBreakerfallBackInfo);
            Assert.Equal(0, circuitBreakerWarning);
            Assert.Equal(HttpStatusCode.ServiceUnavailable, r.StatusCode);

            // Request again. The circuit breaker should be open and not allowing requests to pass through
            var r2 = await invalidHttpClient.GetAsync(uri);

            retryInfo = _loggerFor_SearchHttpClientReturning403.Informations.Where(s => s.StartsWith("Policy retry - it will retry after")).Count();
            onCircuitBreakerfallBackInfo = _loggerFor_SearchHttpClientReturning403.Informations.Where(s => s.StartsWith("On circuit breaker fallback.")).Count();
            circuitBreakerWarning = _loggerFor_SearchHttpClientReturning403.Warnings.Where(s => s.StartsWith("SearchCircuitBreaker logging: Breaking the circuit for")).Count();

            // No other info added 
            Assert.Equal(0, retryInfo);
            Assert.Equal(2, onCircuitBreakerfallBackInfo);
            Assert.Equal(1, circuitBreakerWarning);
            Assert.Equal(HttpStatusCode.ServiceUnavailable, r2.StatusCode);

            // Request again with correct uri. The circuit breaker should be open and not allowing requests to pass through
            var r3 = await invalidHttpClient.GetAsync(validUri);

            retryInfo = _loggerFor_SearchHttpClientReturning403.Informations.Where(s => s.StartsWith("Policy retry - it will retry after")).Count();
            onCircuitBreakerfallBackInfo = _loggerFor_SearchHttpClientReturning403.Informations.Where(s => s.StartsWith("On circuit breaker fallback.")).Count();
            circuitBreakerWarning = _loggerFor_SearchHttpClientReturning403.Warnings.Where(s => s.StartsWith("SearchCircuitBreaker logging: Breaking the circuit for")).Count();

            // No other info added 
            Assert.Equal(0, retryInfo);
            Assert.Equal(3, onCircuitBreakerfallBackInfo);
            Assert.Equal(1, circuitBreakerWarning);
            Assert.Equal(HttpStatusCode.ServiceUnavailable, r2.StatusCode);
        }

        [Fact]
        public async Task TestCircuitBreakerForRecoveringRequests()
        {
            var invalidHttpClient = _services.BuildServiceProvider().GetServices<TestSearchHttpClient>().Where(s => s.BaseAddress == new Uri(_shortInvalidAddress)).ElementAt(0);
            var uri = new Uri($"{invalidHttpClient.BaseAddress}query?q=packageid:Newtonsoft.Json version:12.0.1");
            var validUri = new Uri($"{_validAddress}/query?q=packageid:Newtonsoft.Json version:12.0.1");

            var r = await invalidHttpClient.GetAsync(uri);

            var retryInfo = _loggerFor_InvalidTestSearchHttpClientWithShortCircuitBreakDelay.Informations.Where(s => s.StartsWith("Policy retry - it will retry after")).Count();
            var onCircuitBreakerfallBackInfo = _loggerFor_InvalidTestSearchHttpClientWithShortCircuitBreakDelay.Informations.Where(s => s.StartsWith("On circuit breaker fallback.")).Count();
            var circuitBreakerWarning = _loggerFor_InvalidTestSearchHttpClientWithShortCircuitBreakDelay.Warnings.Where(s => s.StartsWith("SearchCircuitBreaker logging: Breaking the circuit for")).Count();
            var onCircuitBreakerReset = _loggerFor_InvalidTestSearchHttpClientWithShortCircuitBreakDelay.Informations.Where(s => s.StartsWith("SearchCircuitBreaker logging: Call ok! Closed the circuit again!")).Count();
            var onCircuitBreakerHalfOpen = _loggerFor_InvalidTestSearchHttpClientWithShortCircuitBreakDelay.Informations.Where(s => s.StartsWith("SearchCircuitBreaker logging: Half-open: Next call is a trial!")).Count();

            Assert.Equal(2, retryInfo);
            Assert.Equal(1, onCircuitBreakerfallBackInfo);
            Assert.Equal(1, circuitBreakerWarning);
            Assert.Equal(0, onCircuitBreakerReset);
            Assert.Equal(0, onCircuitBreakerHalfOpen);
            Assert.Equal(HttpStatusCode.ServiceUnavailable, r.StatusCode);

            Task.Delay(TimeSpan.FromSeconds(_circuitBreakerShortDelaySeconds)).Wait();

            // Request again with correct uri. The circuit breaker delay should be expired and will do the trial and pass 
            var r2 = await invalidHttpClient.GetAsync(validUri);

            retryInfo = _loggerFor_InvalidTestSearchHttpClientWithShortCircuitBreakDelay.Informations.Where(s => s.StartsWith("Policy retry - it will retry after")).Count();
            onCircuitBreakerfallBackInfo = _loggerFor_InvalidTestSearchHttpClientWithShortCircuitBreakDelay.Informations.Where(s => s.StartsWith("On circuit breaker fallback.")).Count();
            circuitBreakerWarning = _loggerFor_InvalidTestSearchHttpClientWithShortCircuitBreakDelay.Warnings.Where(s => s.StartsWith("SearchCircuitBreaker logging: Breaking the circuit for")).Count();
            onCircuitBreakerReset = _loggerFor_InvalidTestSearchHttpClientWithShortCircuitBreakDelay.Informations.Where(s => s.StartsWith("SearchCircuitBreaker logging: Call ok! Closed the circuit again!")).Count();
            onCircuitBreakerHalfOpen = _loggerFor_InvalidTestSearchHttpClientWithShortCircuitBreakDelay.Informations.Where(s => s.StartsWith("SearchCircuitBreaker logging: Half-open: Next call is a trial!")).Count();

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
            var validClient = _services.BuildServiceProvider().GetServices<TestSearchHttpClient>().Where(s => s.BaseAddress == new Uri(_validAddress)).ElementAt(0);
            var uri = new Uri($"{validClient.BaseAddress}query?q=packageid:Newtonsoft.Json version:12.0.1");

            var r = await validClient.GetAsync(uri);

            var retryInfo = _loggerFor_ValidTestSearchHttpClient.Informations.Where(s => s.StartsWith("Policy retry - it will retry after")).Count();
            var onCircuitBreakerfallBackInfo = _loggerFor_ValidTestSearchHttpClient.Informations.Where(s => s.StartsWith("On circuit breaker fallback.")).Count();
            var circuitBreakerWarning = _loggerFor_ValidTestSearchHttpClient.Warnings.Where(s => s.StartsWith("SearchCircuitBreaker logging: Breaking the circuit for")).Count();
            var onCircuitBreakerReset = _loggerFor_ValidTestSearchHttpClient.Informations.Where(s => s.StartsWith("SearchCircuitBreaker logging: Call ok! Closed the circuit again!")).Count();
            var onCircuitBreakerHalfOpen = _loggerFor_ValidTestSearchHttpClient.Informations.Where(s => s.StartsWith("SearchCircuitBreaker logging: Half-open: Next call is a trial!")).Count();

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

            var retryInfo = _loggerFor_InvalidTestSearchHttpClientRetryCountExpires.Informations.Where(s => s.StartsWith("Policy retry - it will retry after")).Count();
            var onCircuitBreakerfallBackInfo = _loggerFor_InvalidTestSearchHttpClientRetryCountExpires.Informations.Where(s => s.StartsWith("On circuit breaker fallback.")).Count();
            var circuitBreakerWarning = _loggerFor_InvalidTestSearchHttpClientRetryCountExpires.Warnings.Where(s => s.StartsWith("SearchCircuitBreaker logging: Breaking the circuit for")).Count();
            var onCircuitBreakerReset = _loggerFor_InvalidTestSearchHttpClientRetryCountExpires.Informations.Where(s => s.StartsWith("SearchCircuitBreaker logging: Call ok! Closed the circuit again!")).Count();
            var onCircuitBreakerHalfOpen = _loggerFor_InvalidTestSearchHttpClientRetryCountExpires.Informations.Where(s => s.StartsWith("SearchCircuitBreaker logging: Half-open: Next call is a trial!")).Count();

            Assert.Equal(_retryCount_2, retryInfo);
            Assert.Equal(1, onCircuitBreakerfallBackInfo);
            Assert.Equal(0, circuitBreakerWarning);
            Assert.Equal(0, onCircuitBreakerReset);
            Assert.Equal(0, onCircuitBreakerHalfOpen);
            Assert.Equal(HttpStatusCode.ServiceUnavailable, r.StatusCode);
        }

        private static TestHttpHandler MakeTestHttpHandler()
        {
            var handler = new TestHttpHandler();

            handler.Handlers[_longInvalidAddress] = r => new HttpResponseMessage(HttpStatusCode.ServiceUnavailable);
            handler.Handlers[_shortInvalidAddress] = r => new HttpResponseMessage(HttpStatusCode.ServiceUnavailable);
            handler.Handlers[_shortInvalidAddress_2] = r => new HttpResponseMessage(HttpStatusCode.ServiceUnavailable);
            handler.Handlers[_validAddress] = r => new HttpResponseMessage(HttpStatusCode.OK);
            handler.Handlers[_403Address] = r => new HttpResponseMessage(HttpStatusCode.Forbidden);

            return handler;
        }

        private static ServiceCollection ConfigureServices()
        {
            Mock<ITelemetryService> mockITelemetryService = new Mock<ITelemetryService>();
            ITelemetryService telemetryServiceResolver = mockITelemetryService.Object;

            ServiceCollection services = new ServiceCollection();

            _loggerFor_InvalidTestSearchHttpClientWithLongCircuitBreakDelay = new LoggerFor_TestSearchHttpClient();
            _loggerFor_InvalidTestSearchHttpClientWithShortCircuitBreakDelay = new LoggerFor_TestSearchHttpClient();
            _loggerFor_ValidTestSearchHttpClient = new LoggerFor_TestSearchHttpClient();
            _loggerFor_InvalidTestSearchHttpClientRetryCountExpires = new LoggerFor_TestSearchHttpClient();
            _loggerFor_SearchHttpClientReturning403 = new LoggerFor_TestSearchHttpClient();

            services.AddHttpClient<TestSearchHttpClient>(_nameFor_InvalidTestSearchHttpClientWithLongCircuitBreakDelay, c => c.BaseAddress = new Uri(_longInvalidAddress))
                    .AddPolicyHandler(SearchClientPolicies.SearchClientFallBackCircuitBreakerPolicy(_loggerFor_InvalidTestSearchHttpClientWithLongCircuitBreakDelay, "InvalidTestSearchHttpClientWithLongCircuitBreakDelay", telemetryServiceResolver))
                    .AddPolicyHandler(SearchClientPolicies.SearchClientWaitAndRetryPolicy(_retryCount, _waitBetweenRetriesInMilliseconds, _loggerFor_InvalidTestSearchHttpClientWithLongCircuitBreakDelay, "InvalidTestSearchHttpClientWithLongCircuitBreakDelay", telemetryServiceResolver))
                    .AddPolicyHandler(SearchClientPolicies.SearchClientCircuitBreakerPolicy(
                            _circuitBreakerFailAfter,
                            TimeSpan.FromSeconds(_circuitBreakerLongDelaySeconds),
                            _loggerFor_InvalidTestSearchHttpClientWithLongCircuitBreakDelay,
                            "InvalidTestSearchHttpClientWithLongCircuitBreakDelay", telemetryServiceResolver))
                    .AddHttpMessageHandler(() => MakeTestHttpHandler());

            services.AddHttpClient<TestSearchHttpClient>(_nameFor_InvalidTestSearchHttpClientWithShortCircuitBreakDelay, c => c.BaseAddress = new Uri(_shortInvalidAddress))
                    .AddPolicyHandler(SearchClientPolicies.SearchClientFallBackCircuitBreakerPolicy(_loggerFor_InvalidTestSearchHttpClientWithShortCircuitBreakDelay, "InvalidTestSearchHttpClientWithShortCircuitBreakDelay", telemetryServiceResolver))
                    .AddPolicyHandler(SearchClientPolicies.SearchClientWaitAndRetryPolicy(_retryCount, _waitBetweenRetriesInMilliseconds, _loggerFor_InvalidTestSearchHttpClientWithShortCircuitBreakDelay, "InvalidTestSearchHttpClientWithShortCircuitBreakDelay", telemetryServiceResolver))
                    .AddPolicyHandler(SearchClientPolicies.SearchClientCircuitBreakerPolicy(
                            _circuitBreakerFailAfter,
                            TimeSpan.FromSeconds(_circuitBreakerShortDelaySeconds),
                            _loggerFor_InvalidTestSearchHttpClientWithShortCircuitBreakDelay,
                            "InvalidTestSearchHttpClientWithShortCircuitBreakDelay", telemetryServiceResolver))
                    .AddHttpMessageHandler(() => MakeTestHttpHandler());

            services.AddHttpClient<TestSearchHttpClient>(_nameFor_ValidTestSearchHttpClient, c => c.BaseAddress = new Uri(_validAddress))
                    .AddPolicyHandler(SearchClientPolicies.SearchClientFallBackCircuitBreakerPolicy(_loggerFor_ValidTestSearchHttpClient, "InvalidTestSearchHttpClientWithShortCircuitBreakDelay", telemetryServiceResolver))
                    .AddPolicyHandler(SearchClientPolicies.SearchClientWaitAndRetryPolicy(_retryCount, _waitBetweenRetriesInMilliseconds, _loggerFor_ValidTestSearchHttpClient, "InvalidTestSearchHttpClientWithShortCircuitBreakDelay", telemetryServiceResolver))
                    .AddPolicyHandler(SearchClientPolicies.SearchClientCircuitBreakerPolicy(
                           _circuitBreakerFailAfter,
                           TimeSpan.FromSeconds(_circuitBreakerShortDelaySeconds),
                           _loggerFor_ValidTestSearchHttpClient,
                           "InvalidTestSearchHttpClientWithShortCircuitBreakDelay", telemetryServiceResolver))
                    .AddHttpMessageHandler(() => MakeTestHttpHandler());

            services.AddHttpClient<TestSearchHttpClient>(_nameFor_InvalidTestSearchHttpClientRetryCountExpires, c => c.BaseAddress = new Uri(_shortInvalidAddress_2))
                    .AddPolicyHandler(SearchClientPolicies.SearchClientFallBackCircuitBreakerPolicy(_loggerFor_InvalidTestSearchHttpClientRetryCountExpires, "InvalidTestSearchHttpClientRetryCountExpires", telemetryServiceResolver))
                    .AddPolicyHandler(SearchClientPolicies.SearchClientWaitAndRetryPolicy(_retryCount_2, _waitBetweenRetriesInMilliseconds, _loggerFor_InvalidTestSearchHttpClientRetryCountExpires, "InvalidTestSearchHttpClientRetryCountExpires", telemetryServiceResolver))
                    .AddPolicyHandler(SearchClientPolicies.SearchClientCircuitBreakerPolicy(
                           _circuitBreakerFailAfter_2,
                           TimeSpan.FromSeconds(_circuitBreakerShortDelaySeconds),
                           _loggerFor_InvalidTestSearchHttpClientRetryCountExpires,
                           "InvalidTestSearchHttpClientRetryCountExpires", telemetryServiceResolver))
                    .AddHttpMessageHandler(() => MakeTestHttpHandler());

            services.AddHttpClient<TestSearchHttpClient>(_nameFor_SearchHttpClientReturning403, c => c.BaseAddress = new Uri(_403Address))
                    .AddPolicyHandler(SearchClientPolicies.SearchClientFallBackCircuitBreakerPolicy(_loggerFor_SearchHttpClientReturning403, _nameFor_SearchHttpClientReturning403, telemetryServiceResolver))
                    .AddPolicyHandler(SearchClientPolicies.SearchClientWaitAndRetryPolicy(_retryCount, _waitBetweenRetriesInMilliseconds, _loggerFor_SearchHttpClientReturning403, _nameFor_SearchHttpClientReturning403, telemetryServiceResolver))
                    .AddPolicyHandler(SearchClientPolicies.SearchClientCircuitBreakerPolicy(
                            _circuitBreakerFailAfter,
                            TimeSpan.FromSeconds(_circuitBreakerLongDelaySeconds),
                            _loggerFor_SearchHttpClientReturning403,
                            _nameFor_SearchHttpClientReturning403, telemetryServiceResolver))
                    .AddHttpMessageHandler(() => MakeTestHttpHandler());

            return services;
        }

        private class TestHttpHandler : DelegatingHandler
        {
            public Dictionary<string, Func<HttpRequestMessage, HttpResponseMessage>> Handlers { get; } =
                new Dictionary<string, Func<HttpRequestMessage, HttpResponseMessage>>();

            protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            {
                var schemeAndServer = request.RequestUri.GetComponents(UriComponents.SchemeAndServer, UriFormat.Unescaped);
                if (Handlers.TryGetValue(schemeAndServer, out var handler))
                {
                    return Task.FromResult(handler(request));
                }

                throw new NotImplementedException($"Scheme and server '{schemeAndServer}' does not have a handler.");
            }
        }

        private class TestSearchHttpClient : IHttpClientWrapper
        {
            HttpClient _client;

            public TestSearchHttpClient(HttpClient client)
            {
                _client = client;
            }

            public Uri BaseAddress => _client.BaseAddress;

            public async Task<HttpResponseMessage> GetAsync(Uri uri)
            {
                HttpResponseMessage m = null;
                try
                {
                    m = await _client.GetAsync(uri);
                    return m;
                }
                catch (Exception e)
                {
                    var msg = e.Message;
                    throw e;
                }
            }
        }

        private class ValidSearchHttpClient : IHttpClientWrapper
        {
            HttpClient _client;

            public ValidSearchHttpClient(HttpClient client)
            {
                _client = client;
            }

            public int FailIndexForAsyncIndex { get; set; }

            public Uri BaseAddress => _client.BaseAddress;

            public Task<HttpResponseMessage> GetAsync(Uri uri)
            {
                return _client.GetAsync(uri);
            }
        }

        private class LoggerFor_TestSearchHttpClient : ILogger
        {
            public List<string> Warnings = new List<string>();
            public List<string> Informations = new List<string>();

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
                switch(logLevel)
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
