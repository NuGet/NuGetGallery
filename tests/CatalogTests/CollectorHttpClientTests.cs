// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using Moq;
using Newtonsoft.Json.Linq;
using NgTests.Infrastructure;
using NuGet.Services.Metadata.Catalog;
using Xunit;

namespace CatalogTests
{
    public class CollectorHttpClientTests
    {
        private const string TestRawJson = "{\"key\": \"value\"}";
        private const string TestRelativePath = "/index.json";
        private static readonly Uri TestUri = new Uri("http://localhost" + TestRelativePath);

        private readonly MockServerHttpClientHandler _handler;
        private readonly Mock<ITelemetryService> _telemetryService;
        private readonly CollectorHttpClient _target;

        public CollectorHttpClientTests()
        {
            _telemetryService = new Mock<ITelemetryService>();
            _handler = new MockServerHttpClientHandler();

            _target = new CollectorHttpClient(_handler);
        }

        private void AddResponse(HttpStatusCode statusCode)
        {
            _handler.SetAction(TestRelativePath, _ => Task.FromResult(new HttpResponseMessage(statusCode)
            {
                Content = new StringContent(TestRawJson),
            }));
        }

        [Fact]
        public async Task GetJObjectAsync_WhenStatusIsOK_ReturnsParsedJson()
        {
            // Arrange
            AddResponse(HttpStatusCode.OK);

            // Act
            var json = await _target.GetJObjectAsync(TestUri);

            // Assert
            Assert.Equal(JObject.Parse(TestRawJson), json);
        }

        [Fact]
        public async Task GetJObjectAsync_WhenStatusIsNotFound_Throws()
        {
            AddResponse(HttpStatusCode.NotFound);

            var exception = await Assert.ThrowsAsync<Exception>(() => _target.GetJObjectAsync(TestUri));

            Assert.Equal($"GetStringAsync({TestUri})", exception.Message);
            Assert.Equal("Response status code does not indicate success: 404 (Not Found).", exception.InnerException.Message);
        }

        [Fact]
        public async Task GetJObjectAsync_WhenStatusIsInternalServerErrorAndFailureIsPersistent_Throws()
        {
            AddResponse(HttpStatusCode.InternalServerError);

            var exception = await Assert.ThrowsAsync<Exception>(() => _target.GetJObjectAsync(TestUri));

            Assert.Equal($"GetStringAsync({TestUri})", exception.Message);
            Assert.Equal("Maximum retry attempts exhausted.", exception.InnerException.Message);
        }

        [Fact]
        public async Task GetJObjectAsync_WhenStatusIsFirstInternalServerErrorThenOK_ReturnsParsedJson()
        {
            _handler.SetAction(TestRelativePath, _ =>
            {
                _handler.Actions.Clear();

                AddResponse(HttpStatusCode.OK);

                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.InternalServerError));
            });

            var json = await _target.GetJObjectAsync(TestUri);

            Assert.Equal(JObject.Parse(TestRawJson), json);
        }

        [Fact]
        public async Task GetJObjectAsync_WhenStatusIsOKButResponseIsNotJson_Throws()
        {
            _handler.SetAction(TestRelativePath, _ => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("<a/>")
            }));

            var exception = await Assert.ThrowsAsync<Exception>(() => _target.GetJObjectAsync(TestUri));

            Assert.Equal($"GetJObjectAsync({TestUri})", exception.Message);
            Assert.Equal("Unexpected character encountered while parsing value: <. Path '', line 0, position 0.", exception.InnerException.Message);
        }
    }
}