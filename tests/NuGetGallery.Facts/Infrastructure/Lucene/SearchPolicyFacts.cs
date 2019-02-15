// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using Newtonsoft.Json.Linq;
using System.Text;
using System.Threading.Tasks;
using NuGet.Services.Search.Client;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace NuGetGallery.Infrastructure.Lucene
{
    public class SearchPolicyFacts
    {
        private static int _retryCount = 2;
        private static int _circuitBreakerLongDelaySeconds = 600;
        private static int _circuitBreakerShortDelaySeconds = 1;
        private static ServiceCollection _services = null;
        private static readonly object _lockServices = new object();
        private static LoggerFor_TestSearchHttpClient _loggerFor_InvalidTestSearchHttpClientWithLongCircuitBreakDelay;
        private static LoggerFor_TestSearchHttpClient _loggerFor_InvalidTestSearchHttpClientWithShortCircuitBreakDelay;
        private static LoggerFor_TestSearchHttpClient _loggerFor_ValidTestSearchHttpClient;
        private static string _nameFor_InvalidTestSearchHttpClientWithLongCircuitBreakDelay = "InvalidTestSearchHttpClientWithLongCircuitBreakDelay";
        private static string _nameFor_InvalidTestSearchHttpClientWithShortCircuitBreakDelay = "InvalidTestSearchHttpClientWithSWhortCircuitBreakDelay";
        private static string _nameFor_ValidTestSearchHttpClient = "ValidTestSearchHttpClient";
        private static readonly string _longInvalidAddress = "https://api-v2v3search-long.nuget.org";
        private static readonly string _shortInvalidAddress = "https://api-v2v3search-short.nuget.org";
        private static readonly string _validAddress = "https://api-v2v3search-0.nuget.org";

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

            var retryInfo = _loggerFor_InvalidTestSearchHttpClientWithShortCircuitBreakDelay.Informations.Where(s => s.StartsWith("Policy retry - it will retry after")).Count();
            var onCircuitBreakerfallBackInfo = _loggerFor_InvalidTestSearchHttpClientWithShortCircuitBreakDelay.Informations.Where(s => s.StartsWith("On circuit breaker fallback.")).Count();
            var circuitBreakerWarning = _loggerFor_InvalidTestSearchHttpClientWithShortCircuitBreakDelay.Warnings.Where(s => s.StartsWith("SearchCircuitBreaker logging: Breaking the circuit for")).Count();
            var onCircuitBreakerReset = _loggerFor_InvalidTestSearchHttpClientWithShortCircuitBreakDelay.Informations.Where(s => s.StartsWith("SearchCircuitBreaker logging: Call ok! Closed the circuit again!")).Count();
            var onCircuitBreakerHalfOpen = _loggerFor_InvalidTestSearchHttpClientWithShortCircuitBreakDelay.Informations.Where(s => s.StartsWith("SearchCircuitBreaker logging: Half-open: Next call is a trial!")).Count();

            Assert.Equal(0, retryInfo);
            Assert.Equal(0, onCircuitBreakerfallBackInfo);
            Assert.Equal(0, circuitBreakerWarning);
            Assert.Equal(0, onCircuitBreakerReset);
            Assert.Equal(0, onCircuitBreakerHalfOpen);
            Assert.Equal(HttpStatusCode.OK, r.StatusCode);
        }

        private static ILogger<ResilientSearchHttpClient> GetLogger()
        {
            var mockConfiguration = new Mock<ILogger<ResilientSearchHttpClient>>();
            return mockConfiguration.Object;
        }

        private static ServiceCollection ConfigureServices()
        {
            ServiceCollection services = new ServiceCollection();
            _loggerFor_InvalidTestSearchHttpClientWithLongCircuitBreakDelay = new LoggerFor_TestSearchHttpClient();
            _loggerFor_InvalidTestSearchHttpClientWithShortCircuitBreakDelay = new LoggerFor_TestSearchHttpClient();
            _loggerFor_ValidTestSearchHttpClient = new LoggerFor_TestSearchHttpClient();

            services.AddHttpClient<TestSearchHttpClient>(_nameFor_InvalidTestSearchHttpClientWithLongCircuitBreakDelay, c =>
                         c.BaseAddress = new Uri(_longInvalidAddress))
                    .AddPolicyHandler(SearchClientPolicies.SearchClientFallBackCircuitBreakerPolicy(_loggerFor_InvalidTestSearchHttpClientWithLongCircuitBreakDelay))
                    .AddPolicyHandler(SearchClientPolicies.SearchClientWaitAndRetryForeverPolicy(_loggerFor_InvalidTestSearchHttpClientWithLongCircuitBreakDelay))
                    .AddPolicyHandler(SearchClientPolicies.SearchClientCircuitBreakerPolicy(
                            _retryCount,
                            TimeSpan.FromSeconds(_circuitBreakerLongDelaySeconds),
                            _loggerFor_InvalidTestSearchHttpClientWithLongCircuitBreakDelay));

            services.AddHttpClient<TestSearchHttpClient>(_nameFor_InvalidTestSearchHttpClientWithShortCircuitBreakDelay, c =>
                         c.BaseAddress = new Uri(_shortInvalidAddress))
                    .AddPolicyHandler(SearchClientPolicies.SearchClientFallBackCircuitBreakerPolicy(_loggerFor_InvalidTestSearchHttpClientWithShortCircuitBreakDelay))
                    .AddPolicyHandler(SearchClientPolicies.SearchClientWaitAndRetryForeverPolicy(_loggerFor_InvalidTestSearchHttpClientWithShortCircuitBreakDelay))
                    .AddPolicyHandler(SearchClientPolicies.SearchClientCircuitBreakerPolicy(
                            _retryCount,
                            TimeSpan.FromSeconds(_circuitBreakerShortDelaySeconds),
                            _loggerFor_InvalidTestSearchHttpClientWithShortCircuitBreakDelay));

            services.AddHttpClient<TestSearchHttpClient>(_nameFor_ValidTestSearchHttpClient, c =>
                        c.BaseAddress = new Uri(_validAddress))
                   .AddPolicyHandler(SearchClientPolicies.SearchClientFallBackCircuitBreakerPolicy(_loggerFor_ValidTestSearchHttpClient))
                   .AddPolicyHandler(SearchClientPolicies.SearchClientWaitAndRetryForeverPolicy(_loggerFor_ValidTestSearchHttpClient))
                   .AddPolicyHandler(SearchClientPolicies.SearchClientCircuitBreakerPolicy(
                           _retryCount,
                           TimeSpan.FromSeconds(_circuitBreakerShortDelaySeconds),
                           _loggerFor_ValidTestSearchHttpClient));

            return services;
        }

        private static IResilientSearchClient GetResilientSearchClient(string primaryBaseAddress,
            string secondaryBaseAddress,
            HttpResponseMessage getAsyncResultMessage1,
            HttpResponseMessage getAsyncResultMessage2,
            out Mock<ISearchHttpClient> mockISearchHttpClient1,
            out Mock<ISearchHttpClient> mockISearchHttpClient2)
        {
            var mockTelemetryService = new Mock<ITelemetryService>();
            mockISearchHttpClient1 = new Mock<ISearchHttpClient>();
            mockISearchHttpClient1.SetupGet(x => x.BaseAddress).Returns(new Uri(primaryBaseAddress));
            mockISearchHttpClient1.Setup(x => x.GetAsync(It.IsAny<Uri>())).ReturnsAsync(getAsyncResultMessage1);

            mockISearchHttpClient2 = new Mock<ISearchHttpClient>();
            mockISearchHttpClient2.SetupGet(x => x.BaseAddress).Returns(new Uri(secondaryBaseAddress));
            mockISearchHttpClient2.Setup(x => x.GetAsync(It.IsAny<Uri>())).ReturnsAsync(getAsyncResultMessage2);

            List<ISearchHttpClient> clients = new List<ISearchHttpClient>() { mockISearchHttpClient1.Object, mockISearchHttpClient2.Object };
            return new ResilientSearchHttpClient(clients, GetLogger(), mockTelemetryService.Object);
        }

        private static HttpResponseMessage GetResponseMessage(Uri uri, HttpStatusCode statusCode)
        {
            string path = uri.AbsolutePath;
            string queryString = uri.Query;

            var content = new JObject(
                           new JProperty("queryString", queryString),
                           new JProperty("path", path));

            return new HttpResponseMessage()
            {
                Content = new StringContent(content.ToString(), Encoding.UTF8, CoreConstants.TextContentType),
                RequestMessage = new HttpRequestMessage(HttpMethod.Get, $"{path}/{queryString}"),
                StatusCode = statusCode
            };

        }

        private class TestSearchHttpClient : ISearchHttpClient
        {
            HttpClient _client;

            public TestSearchHttpClient(HttpClient client)
            {
                _client = client;
            }

            public Uri BaseAddress => _client.BaseAddress;

            public Task<HttpResponseMessage> GetAsync(Uri uri)
            {
                return _client.GetAsync(uri);
            }
        }

        private class ValidSearchHttpClient : ISearchHttpClient
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

        private class LoggerForValidSearchHttpClient : ILogger
        {
            public IDisposable BeginScope<TState>(TState state)
            {
                throw new NotImplementedException();
            }

            public bool IsEnabled(LogLevel logLevel)
            {
                throw new NotImplementedException();
            }

            public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception exception, Func<TState, Exception, string> formatter)
            {
                throw new NotImplementedException();
            }
        }
    }
}
