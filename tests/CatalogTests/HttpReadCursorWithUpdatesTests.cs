// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Net.Http;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Moq;
using NuGet.Services.Metadata.Catalog;
using Xunit;

namespace CatalogTests
{
    public class HttpReadCursorWithUpdatesTests
    {
        private readonly HttpReadCursorWithUpdates _cursor;
        private readonly HttpResponseMessage _response;

        public HttpReadCursorWithUpdatesTests()
        {
            _cursor = new HttpReadCursorWithUpdates(minIntervalBeforeToReadCursorUpdateValue: TimeSpan.FromMinutes(1) + TimeSpan.FromSeconds(1),
                new Uri("https://test"), Mock.Of<ILogger>());
            _response = new HttpResponseMessage();
        }

        [Fact]
        public async Task GetValueInJsonAsync()
        {
            _response.Headers.Date = new DateTime(2026, 1, 1, 1, 0, 30, DateTimeKind.Utc);
            _response.Content = new StringContent("{\"value\":\"2026-01-01T01:00:00.0000000\"," +
                                                   "\"updates\":[{\"timeStamp\":\"2026-01-01T00:59:30.0000000Z\",\"value\":\"2026-01-01T00:59:00.0000000\"}," +
                                                                "{\"timeStamp\":\"2026-01-01T00:58:30.0000000Z\",\"value\":\"2026-01-01T00:58:00.0000000\"}," +
                                                                "{\"timeStamp\":\"2026-01-01T00:57:30.0000000Z\",\"value\":\"2026-01-01T00:57:00.0000000\"}]}");

            Assert.Equal("{\"value\": \"2026-01-01T00:58:00.0000000\"}", await _cursor.GetValueInJsonAsync(_response));
        }

        [Theory]
        [InlineData("{\"value\":\"2026-01-01T01:00:00.0000000\"}")]
        [InlineData("{\"value\":\"2026-01-01T01:00:00.0000000\",\"updates\":[]}")]
        [InlineData("{\"value\":\"2026-01-01T01:00:00.0000000\"," +
                     "\"updates\":[{\"timeStamp\":\"2026-01-01T00:59:30.0000000Z\",\"value\":\"2026-01-01T00:59:00.0000000\"}]}")]
        public async Task GetValueInJsonAsync_UnableToFindCursorUpdate(string content)
        {
            _response.Headers.Date = new DateTime(2026, 1, 1, 1, 0, 30, DateTimeKind.Utc);
            _response.Content = new StringContent(content);

            var exception = await Assert.ThrowsAsync<InvalidOperationException>(() => _cursor.GetValueInJsonAsync(_response));
            Assert.Equal("Unable to find the cursor update.", exception.Message);
        }

        [Fact]
        public async Task GetValueInJsonAsync_WithoutStorageDateTime()
        {
            _response.Headers.Date = null;

            var exception = await Assert.ThrowsAsync<ArgumentNullException>(() => _cursor.GetValueInJsonAsync(_response));
            Assert.Equal("storageDateTimeInUtc", exception.ParamName);
        }
    }
}
