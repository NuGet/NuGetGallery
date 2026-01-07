// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Net.Http;
using System.Web.Http.OData;
using System.Web.Http.OData.Query;
using Moq;
using NuGetGallery.OData.QueryFilter;
using Xunit;

namespace NuGetGallery.OData.Filter
{
    public class ODataFilterFacts
    {
        const string Host = "https://localhost:8081/";

        private readonly ODataQueryVerifier queryVerifier;

        public ODataFilterFacts()
        {
            var telemetryService = new Mock<ITelemetryService>().Object;
            queryVerifier = ODataQueryVerifier.Build(telemetryService);
        }

        [Theory]
        [InlineData("apiv2getupdates.json")]
        [InlineData("apiv2packages.json")]
        [InlineData("apiv2search.json")]
        public void ODataNoOperatorsAreAllowedV2(string apiResourceFileName)
        {
            // Arrange
            var odataOptions = new ODataQueryOptions<V2FeedPackage>(
                new ODataQueryContext(NuGetODataV2FeedConfig.GetEdmModel(), typeof(V2FeedPackage)),
                new HttpRequestMessage(HttpMethod.Get, Host));

            var queryFilter = new ODataQueryFilter(apiResourceFileName);

            // Act
            var result = queryVerifier.AreODataOptionsAllowed(odataOptions, queryFilter, true, "TestContext");

            // Assert
            Assert.True(result, "A request with no OData operators should be allowed.");
        }

        [Theory]
        [InlineData("apiv1packages.json")]
        [InlineData("apiv1search.json")]
        public void ODataNoOperatorsAreAllowedV1(string apiResourceFileName)
        {
            // Arrange
            var odataOptions = new ODataQueryOptions<V1FeedPackage>(
                new ODataQueryContext(NuGetODataV1FeedConfig.GetEdmModel(), typeof(V1FeedPackage)),
                new HttpRequestMessage(HttpMethod.Get, Host));

            var queryFilter = new ODataQueryFilter(apiResourceFileName);

            // Act
            var result = queryVerifier.AreODataOptionsAllowed(odataOptions, queryFilter, true, "TestContext");

            // Assert
            Assert.True(result, "A request with no OData operators should be allowed.");
        }
    }
}
