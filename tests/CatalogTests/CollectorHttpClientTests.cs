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
        public async Task ReturnsParsedJson()
        {
            // Arrange
            AddResponse(HttpStatusCode.OK);

            // Act
            var json = await _target.GetJObjectAsync(TestUri);

            // Assert
            Assert.Equal(JObject.Parse(TestRawJson), json);
        }
    }
}
