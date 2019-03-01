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
    public class V1FeedPackageAnnotationStrategyFacts
    {
        private readonly string _contentType = "application/zip";

        [Fact]
        public void CanNotAnnotateNullObject()
        {
            // Arrange
            var annotationStrategy = new V1FeedPackageAnnotationStrategy(_contentType);

            // Act
            var canAnnotate = annotationStrategy.CanAnnotate(null);

            // Assert
            Assert.False(canAnnotate);
        }

        [Fact]
        public void CanNotAnnotateV2FeedPackage()
        {
            // Arrange
            var v2FeedPackage = new V2FeedPackage();
            var annotationStrategy = new V1FeedPackageAnnotationStrategy(_contentType);

            // Act
            var canAnnotate = annotationStrategy.CanAnnotate(v2FeedPackage);

            // Assert
            Assert.False(canAnnotate);
        }

        [Fact]
        public void SetsAtomEntryMetadataAnnotation()
        {
            // Arrange
            var v1FeedPackage = new V1FeedPackage()
            {
                Id = "SomePackageId",
                Version = "1.0.0",
                Title = "Title",
                Authors = ".NET Foundation",
                LastUpdated = DateTime.UtcNow,
                Summary = "Summary"
            };
            var annotationStrategy = new V1FeedPackageAnnotationStrategy(_contentType);
            var oDataEntry = new ODataEntry();
            var request = CreateHttpRequestMessage();

            var expectedAtomEntryMetadataAnnotation = new AtomEntryMetadata()
            {
                Title = v1FeedPackage.Title,
                Authors = new[] { new AtomPersonMetadata { Name = v1FeedPackage.Authors } },
                Updated = v1FeedPackage.LastUpdated,
                Summary = v1FeedPackage.Summary
            };

            // Act
            annotationStrategy.Annotate(request, oDataEntry, v1FeedPackage);

            var actualAtomEntryMetadataAnnotation = oDataEntry.GetAnnotation<AtomEntryMetadata>();

            // Assert
            Assert.Equal(expectedAtomEntryMetadataAnnotation.Title.Text, actualAtomEntryMetadataAnnotation.Title.Text);
            Assert.Equal(expectedAtomEntryMetadataAnnotation.Summary.Text, actualAtomEntryMetadataAnnotation.Summary.Text);
            Assert.Equal(expectedAtomEntryMetadataAnnotation.Authors.Single().Name, actualAtomEntryMetadataAnnotation.Authors.Single().Name);
            Assert.Equal(expectedAtomEntryMetadataAnnotation.Updated, actualAtomEntryMetadataAnnotation.Updated);
        }

        [Fact]
        public void SetsMediaResourceAnnotation()
        {
            // Arrange
            var v1FeedPackage = new V1FeedPackage()
            {
                Id = "SomePackageId",
                Version = "1.0.0",
                Title = "Title",
                Authors = ".NET Foundation",
                LastUpdated = DateTime.UtcNow,
                Summary = "Summary"
            };
            var annotationStrategy = new V1FeedPackageAnnotationStrategy(_contentType);
            var oDataEntry = new ODataEntry();
            var request = CreateHttpRequestMessage();

            // Act
            annotationStrategy.Annotate(request, oDataEntry, v1FeedPackage);

            // Assert
            Assert.Equal(_contentType, oDataEntry.MediaResource.ContentType);
            Assert.Equal("https://localhost/api/v1/package/SomePackageId/1.0.0", oDataEntry.MediaResource.ReadLink.ToString());
        }

        private static HttpRequestMessage CreateHttpRequestMessage()
        {
            var downloadPackageRoute = new HttpRoute(
                "api/v1/package/{id}/{version}",
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
            routeCollection.Add("v1" + RouteName.DownloadPackage, downloadPackageRoute);

            var httpConfiguration = new HttpConfiguration(routeCollection);

            var request = new HttpRequestMessage(HttpMethod.Get, "https://localhost/api/v1/Packages");
            request.SetConfiguration(httpConfiguration);
            return request;
        }
    }
}
