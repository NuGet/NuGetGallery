// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using Microsoft.Data.OData;
using Microsoft.Data.OData.Atom;
using System;
using System.Linq;
using System.Net.Http;
using System.Web.Http;
using System.Web.Http.Routing;
using System.Web.Mvc;
using Xunit;

namespace NuGetGallery.OData.Serializers
{
    public class V2FeedPackageAnnotationStrategyFacts
    {
        private readonly string _contentType = "application/zip";

        [Fact]
        public void CanNotAnnotateNullObject()
        {
            // Arrange
            var annotationStrategy = new V2FeedPackageAnnotationStrategy(_contentType);

            // Act
            var canAnnotate = annotationStrategy.CanAnnotate(null);

            // Assert
            Assert.False(canAnnotate);
        }

        [Fact]
        public void CanNotAnnotateV1FeedPackage()
        {
            // Arrange
            var v1FeedPackage = new V1FeedPackage();
            var annotationStrategy = new V2FeedPackageAnnotationStrategy(_contentType);

            // Act
            var canAnnotate = annotationStrategy.CanAnnotate(v1FeedPackage);

            // Assert
            Assert.False(canAnnotate);
        }

        [Fact]
        public void SetsAtomEntryMetadataAnnotation()
        {
            // Arrange
            var v2FeedPackage = new V2FeedPackage()
            {
                Id = "SomePackageId",
                Version = "1.0.0",
                Title = "Title",
                Authors = ".NET Foundation",
                LastUpdated = DateTime.UtcNow,
                Summary = "Summary"
            };
            var annotationStrategy = new V2FeedPackageAnnotationStrategy(_contentType);
            var oDataEntry = new ODataEntry();
            var request = CreateHttpRequestMessage("https://localhost/api/v2/Packages");

            var expectedAtomEntryMetadataAnnotation = new AtomEntryMetadata()
            {
                Title = v2FeedPackage.Id,
                Authors = new[] { new AtomPersonMetadata { Name = v2FeedPackage.Authors } },
                Updated = v2FeedPackage.LastUpdated,
                Summary = v2FeedPackage.Summary
            };

            // Act
            annotationStrategy.Annotate(request, oDataEntry, v2FeedPackage);

            var actualAtomEntryMetadataAnnotation = oDataEntry.GetAnnotation<AtomEntryMetadata>();

            // Assert
            Assert.Equal(expectedAtomEntryMetadataAnnotation.Title.Text, actualAtomEntryMetadataAnnotation.Title.Text);
            Assert.Equal(expectedAtomEntryMetadataAnnotation.Summary.Text, actualAtomEntryMetadataAnnotation.Summary.Text);
            Assert.Equal(expectedAtomEntryMetadataAnnotation.Authors.Single().Name, actualAtomEntryMetadataAnnotation.Authors.Single().Name);
            Assert.Equal(expectedAtomEntryMetadataAnnotation.Updated, actualAtomEntryMetadataAnnotation.Updated);
        }

        [Theory]
        [InlineData("https://localhost/api/v2/Packages")]
        [InlineData("https://localhost/api/v2/Packages()")]
        [InlineData("https://localhost/api/v2/Packages(Id='SomePackageId',Version='1.0.0')")]
        [InlineData("https://localhost/api/v2/FindPackagesById()?id='SomePackageId'")]
        [InlineData("https://localhost/api/v2/FindPackagesById(Id='SomePackageId')")]
        [InlineData("https://localhost/api/v2/Search()?searchTerm='SomePackageId'")]
        [InlineData("https://localhost/api/v2/GetUpdates()?packageIds=='SomePackageId'")]
        public void NormalizesNavigationLinksWhenSet(string requestUri)
        {
            // Arrange
            var v2FeedPackage = new V2FeedPackage()
            {
                Id = "SomePackageId",
                Version = "1.0.0",
                Title = "Title",
                Authors = ".NET Foundation",
                LastUpdated = DateTime.UtcNow,
                Summary = "Summary"
            };
            var annotationStrategy = new V2FeedPackageAnnotationStrategy(_contentType);
            var oDataEntry = new ODataEntry();
            var dummyIdLink = new Uri("https://localhost");
            oDataEntry.Id = dummyIdLink.ToString();
            oDataEntry.EditLink = dummyIdLink;
            oDataEntry.ReadLink = dummyIdLink;

            var request = CreateHttpRequestMessage(requestUri);
            var expectedNormalizedLink = "https://localhost/api/v2/Packages(Id='SomePackageId',Version='1.0.0')";

            // Act
            annotationStrategy.Annotate(request, oDataEntry, v2FeedPackage);

            // Assert
            Assert.Equal(expectedNormalizedLink, oDataEntry.ReadLink.ToString());
            Assert.Equal(expectedNormalizedLink, oDataEntry.EditLink.ToString());
            Assert.Equal(expectedNormalizedLink, oDataEntry.Id.ToString());
        }

        [Fact]
        public void SetsMediaResourceAnnotation()
        {
            // Arrange
            var v2FeedPackage = new V2FeedPackage()
            {
                Id = "SomePackageId",
                Version = "1.0.0",
                Title = "Title",
                Authors = ".NET Foundation",
                LastUpdated = DateTime.UtcNow,
                Summary = "Summary"
            };
            var annotationStrategy = new V2FeedPackageAnnotationStrategy(_contentType);
            var oDataEntry = new ODataEntry();
            var request = CreateHttpRequestMessage("https://localhost/api/v2/Packages");

            // Act
            annotationStrategy.Annotate(request, oDataEntry, v2FeedPackage);

            // Assert
            Assert.Equal(_contentType, oDataEntry.MediaResource.ContentType);
            Assert.Equal("https://localhost/api/v2/package/SomePackageId/1.0.0", oDataEntry.MediaResource.ReadLink.ToString());
        }

        private static HttpRequestMessage CreateHttpRequestMessage(string requestUri)
        {
            var downloadPackageRoute = new HttpRoute(
                "api/v2/package/{id}/{version}",
                defaults: new HttpRouteValueDictionary(
                    new
                    {
                        controller = "Api",
                        action = ActionName.GetPackageApi,
                        version = UrlParameter.Optional
                    }),
                constraints: new HttpRouteValueDictionary(
                    new
                    {
                        httpMethod = new HttpMethodConstraint(HttpMethod.Get)
                    }));

            var routeCollection = new HttpRouteCollection();
            routeCollection.Add("v2" + RouteName.DownloadPackage, downloadPackageRoute);

            var httpConfiguration = new HttpConfiguration(routeCollection);

            var request = new HttpRequestMessage(HttpMethod.Get, requestUri);
            request.SetConfiguration(httpConfiguration);
            return request;
        }
    }
}
