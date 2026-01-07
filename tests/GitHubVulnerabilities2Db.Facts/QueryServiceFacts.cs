// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading;
using System.Threading.Tasks;
using GitHubVulnerabilities2Db.Configuration;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using NuGet.Services.GitHub.GraphQL;
using NuGetGallery.TestUtils;
using Xunit;
using Xunit.Abstractions;

namespace GitHubVulnerabilities2Db.Facts
{
	public class QueryServiceFacts
	{
		private readonly GitHubVulnerabilities2DbConfiguration _configuration;
		private readonly MockHandler _mockHandler;
		private readonly QueryService _service;
		private Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> _handlerFunction;

		public QueryServiceFacts(ITestOutputHelper output)
		{
			_configuration = new GitHubVulnerabilities2DbConfiguration
			{
				GitHubPersonalAccessToken = "fake-token",
				UserAgent = "NuGet.GitHubVulnerabilities2Db",
				RetryDelay = TimeSpan.Zero,
			};
			_handlerFunction = (r, c) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
			{
				Content = new StringContent("{}")
			});
			_mockHandler = new MockHandler((r, c) => _handlerFunction(r, c));

			var loggerFactory = new LoggerFactory().AddXunit(output);
			_service = new QueryService(_configuration, new HttpClient(_mockHandler), loggerFactory.CreateLogger<QueryService>());
		}

		[Fact]
		public async Task QueryAsync_ReturnsDeserializedResponse_OnSuccess()
		{
			// Arrange
			var expected = new QueryResponse { Data = new QueryResponseData() };
			_handlerFunction = (req, ct) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
			{
				Content = new StringContent(JsonConvert.SerializeObject(expected))
			});

			// Act
			var result = await _service.QueryAsync("query { test }", CancellationToken.None);

			// Assert
			Assert.NotNull(result);
			Assert.NotNull(result.Data);
		}

		[Fact]
		public async Task QueryAsync_RetriesOn5xx_ThenSucceeds()
		{
			// Arrange
			var callCount = 0;
			_handlerFunction = async (req, ct) =>
			{
				await Task.Yield();
				callCount++;
				if (callCount < 3)
				{
					return new HttpResponseMessage(HttpStatusCode.InternalServerError);
				}
				return new HttpResponseMessage(HttpStatusCode.OK)
				{
					Content = new StringContent(JsonConvert.SerializeObject(new QueryResponse { Data = new QueryResponseData() }))
				};
			};

			// Act
			var result = await _service.QueryAsync("query { test }", CancellationToken.None);

			// Assert
			Assert.NotNull(result);
			Assert.Equal(3, callCount);
		}

		[Fact]
		public async Task QueryAsync_RetriesOn5xx_ThenFails()
		{
			// Arrange
			var callCount = 0;
			_handlerFunction = async (req, ct) =>
			{
				await Task.Yield();
				callCount++;
				return new HttpResponseMessage(HttpStatusCode.InternalServerError);
			};

			// Act & Assert
			await Assert.ThrowsAsync<HttpRequestException>(() => _service.QueryAsync("query { test }", CancellationToken.None));
			Assert.Equal(5, callCount);
		}

		[Fact]
		public async Task QueryAsync_DoesNotRetryOn4xx()
		{
			// Arrange
			var callCount = 0;
			_handlerFunction = (req, ct) =>
			{
				callCount++;
				return Task.FromResult(new HttpResponseMessage(HttpStatusCode.BadRequest));
			};

			// Act & Assert
			await Assert.ThrowsAsync<HttpRequestException>(() => _service.QueryAsync("query { test }", CancellationToken.None));
			Assert.Equal(1, callCount);
		}

		[Fact]
		public async Task QueryAsync_ThrowsIfCancelled()
		{
			// Arrange
			var cts = new CancellationTokenSource();
			cts.Cancel();
			_handlerFunction = (req, ct) =>
			{
				ct.ThrowIfCancellationRequested();
				return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK));
			};

			// Act & Assert
			await Assert.ThrowsAnyAsync<OperationCanceledException>(() => _service.QueryAsync("query { test }", cts.Token));
		}

		[Fact]
		public async Task QueryAsync_SetsUserAgentAndAuthorizationHeaders()
		{
			// Arrange
			HttpRequestMessage capturedRequest = null;
			_handlerFunction = (req, ct) =>
			{
				capturedRequest = req;
				return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
				{
					Content = new StringContent(JsonConvert.SerializeObject(new QueryResponse { Data = new QueryResponseData() }))
				});
			};

			// Act
			await _service.QueryAsync("query { test }", CancellationToken.None);

			// Assert
			Assert.NotNull(capturedRequest);
			Assert.Equal("Bearer", capturedRequest.Headers.Authorization?.Scheme);
			Assert.Equal(_configuration.GitHubPersonalAccessToken, capturedRequest.Headers.Authorization?.Parameter);
			Assert.Contains(_configuration.UserAgent, capturedRequest.Headers.UserAgent.ToString());
		}

		[Fact]
		public async Task QueryAsync_ErrorStatusCodeIsRejected()
		{
            // Arrange
            var callCount = 0;
            _handlerFunction = (req, ct) =>
            {
                callCount++;
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.BadRequest)
                {
                    Content = new StringContent("{}")
                });
            };

			// Act & Assert
			await Assert.ThrowsAsync<HttpRequestException>(() => _service.QueryAsync("query { test }", CancellationToken.None));
            Assert.Equal(1, callCount);
		}

		[Fact]
		public async Task QueryAsync_ErrorResponseJsonIsRejected()
		{
			// Arrange
			var response = new QueryResponse { Errors = new List<QueryError> { new QueryError { Message = "Query = not great" } } };
			_handlerFunction = (req, ct) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
			{
				Content = new StringContent(JsonConvert.SerializeObject(response))
			});

			// Act & Assert
			await Assert.ThrowsAsync<InvalidOperationException>(() => _service.QueryAsync("query { test }", CancellationToken.None));
		}
	}
}
