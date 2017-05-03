// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Net.Http;
using System.Web.Http.OData;
using System.Web.Http.OData.Formatter.Serialization;
using System.Web.Http.Routing;
using Microsoft.Data.OData;
using Microsoft.Data.OData.Atom;

namespace NuGetGallery.OData.Serializers
{
    public class NuGetEntityTypeSerializer
        : ODataEntityTypeSerializer
    {
        private readonly string _contentType;

        public NuGetEntityTypeSerializer(ODataSerializerProvider serializerProvider)
            : base(serializerProvider)
        {
            _contentType = "application/zip";
        }

        public override ODataEntry CreateEntry(SelectExpandNode selectExpandNode, EntityInstanceContext entityInstanceContext)
        {
            var entry = base.CreateEntry(selectExpandNode, entityInstanceContext);

            TryAnnotateV1FeedPackage(entry, entityInstanceContext);
            TryAnnotateV2FeedPackage(entry, entityInstanceContext);

            return entry;
        }

        private void TryAnnotateV1FeedPackage(ODataEntry entry, EntityInstanceContext entityInstanceContext)
        {
            var instance = entityInstanceContext.EntityInstance as V1FeedPackage;
            if (instance != null)
            {
                // Set Atom entry metadata
                var atomEntryMetadata = new AtomEntryMetadata();
                atomEntryMetadata.Title = instance.Title;
                if (!string.IsNullOrEmpty(instance.Authors))
                {
                    atomEntryMetadata.Authors = new[] { new AtomPersonMetadata { Name = instance.Authors } };
                }
                if (instance.LastUpdated > DateTime.MinValue)
                {
                    atomEntryMetadata.Updated = instance.LastUpdated;
                }
                if (!string.IsNullOrEmpty(instance.Summary))
                {
                    atomEntryMetadata.Summary = instance.Summary;
                }
                entry.SetAnnotation(atomEntryMetadata);

                // Add package download link
                entry.MediaResource = new ODataStreamReferenceValue
                {
                    ContentType = ContentType,
                    ReadLink = BuildLinkForStreamProperty("v1", instance.Id, instance.Version, entityInstanceContext.Request)
                };
            }
        }

        private void TryAnnotateV2FeedPackage(ODataEntry entry, EntityInstanceContext entityInstanceContext)
        {
            var instance = entityInstanceContext.EntityInstance as V2FeedPackage;
            if (instance != null)
            {
                // Patch links to use normalized versions
                var normalizedVersion = NuGetVersionNormalizer.Normalize(instance.Version);
                NormalizeNavigationLinks(entry, entityInstanceContext.Request, instance, normalizedVersion);

                // Set Atom entry metadata
                var atomEntryMetadata = new AtomEntryMetadata();
                atomEntryMetadata.Title = instance.Id;
                if (!string.IsNullOrEmpty(instance.Authors))
                {
                    atomEntryMetadata.Authors = new[] { new AtomPersonMetadata { Name = instance.Authors } };
                }
                if (instance.LastUpdated > DateTime.MinValue)
                {
                    atomEntryMetadata.Updated = instance.LastUpdated;
                }
                if (!string.IsNullOrEmpty(instance.Summary))
                {
                    atomEntryMetadata.Summary = instance.Summary;
                }
                entry.SetAnnotation(atomEntryMetadata);

                // Add package download link
                entry.MediaResource = new ODataStreamReferenceValue
                {
                    ContentType = ContentType,
                    ReadLink = BuildLinkForStreamProperty("v2", instance.Id, normalizedVersion, entityInstanceContext.Request)
                };
            }
        }

        private static void NormalizeNavigationLinks(ODataEntry entry, HttpRequestMessage request, V2FeedPackage instance, string normalizedVersion)
        {
            var idLink = BuildIdLink(instance.Id, normalizedVersion, request);

            if (entry.ReadLink != null)
            {
                entry.ReadLink = idLink;
            }

            if (entry.EditLink != null)
            {
                entry.EditLink = idLink;
            }

            if (entry.Id != null)
            {
                entry.Id = idLink.ToString();
            }
        }

        public string ContentType
        {
            get { return _contentType; }
        }

        private static Uri BuildLinkForStreamProperty(string routePrefix, string id, string version, HttpRequestMessage request)
        {
            var url = new UrlHelper(request);
            var result = url.Route(routePrefix + RouteName.DownloadPackage, new { id, version });

            var builder = new UriBuilder(request.RequestUri);
            builder.Path = version == null ? EnsureTrailingSlash(result) : result;
            builder.Query = string.Empty;

            return builder.Uri;
        }

        private static Uri BuildIdLink(string id, string version, HttpRequestMessage request)
        {
            return new Uri($"{request.RequestUri.Scheme}://{request.RequestUri.Host}{request.RequestUri.LocalPath}(Id='{id}',Version='{version}')");
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