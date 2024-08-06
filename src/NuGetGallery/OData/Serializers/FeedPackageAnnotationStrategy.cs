// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using Microsoft.Data.OData;
using System;
using System.Net.Http;
using System.Web.Http.Routing;

namespace NuGetGallery.OData.Serializers
{
    internal abstract class FeedPackageAnnotationStrategy<TFeedPackage>
        : IFeedPackageAnnotationStrategy
    {
        private readonly string _contentType;

        protected FeedPackageAnnotationStrategy(string contentType)
        {
            if (string.IsNullOrEmpty(contentType))
            {
                throw new ArgumentException(nameof(contentType));
            }

            _contentType = contentType;
        }

        protected string ContentType => _contentType;

        public bool CanAnnotate(object entityInstance)
        {
            return entityInstance != null && entityInstance is TFeedPackage;
        }

        public abstract void Annotate(HttpRequestMessage request, ODataEntry entry, object entityInstance);

        protected static Uri BuildLinkForStreamProperty(string routePrefix, string id, string version, HttpRequestMessage request)
        {
            var url = new UrlHelper(request);
            var result = url.Route(routePrefix + RouteName.DownloadPackage, new { id, version });

            var builder = new UriBuilder(request.RequestUri);
            builder.Path = version == null ? EnsureTrailingSlash(result) : result;
            builder.Query = string.Empty;

            return builder.Uri;
        }

        private static string EnsureTrailingSlash(string url)
        {
            if (url != null && !url.EndsWith("/", StringComparison.OrdinalIgnoreCase))
            {
                return url + '/';
            }

            return url;
        }
    }
}