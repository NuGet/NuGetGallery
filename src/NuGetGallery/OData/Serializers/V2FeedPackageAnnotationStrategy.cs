// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using Microsoft.Data.OData;
using Microsoft.Data.OData.Atom;
using System;
using System.Net.Http;

namespace NuGetGallery.OData.Serializers
{
    internal class V2FeedPackageAnnotationStrategy
        : FeedPackageAnnotationStrategy<V2FeedPackage>
    {
        public V2FeedPackageAnnotationStrategy(string contentType)
            : base(contentType)
        {
        }

        public override void Annotate(HttpRequestMessage request, ODataEntry entry, object entityInstance)
        {
            var feedPackage = entityInstance as V2FeedPackage;
            if (feedPackage == null)
            {
                return;
            }

            // Patch links to use normalized versions
            var normalizedVersion = NuGetVersionFormatter.Normalize(feedPackage.Version);
            NormalizeNavigationLinks(entry, request, feedPackage, normalizedVersion);

            // Set Atom entry metadata
            var atomEntryMetadata = new AtomEntryMetadata();
            atomEntryMetadata.Title = feedPackage.Id;

            if (!string.IsNullOrEmpty(feedPackage.Authors))
            {
                atomEntryMetadata.Authors = new[] { new AtomPersonMetadata { Name = feedPackage.Authors } };
            }

            if (feedPackage.LastUpdated > DateTime.MinValue)
            {
                atomEntryMetadata.Updated = feedPackage.LastUpdated;
            }

            if (!string.IsNullOrEmpty(feedPackage.Summary))
            {
                atomEntryMetadata.Summary = feedPackage.Summary;
            }

            entry.SetAnnotation(atomEntryMetadata);

            // Add package download link
            entry.MediaResource = new ODataStreamReferenceValue
            {
                ContentType = ContentType,
                ReadLink = BuildLinkForStreamProperty("v2", feedPackage.Id, normalizedVersion, request)
            };
        }

        private static void NormalizeNavigationLinks(ODataEntry entry, HttpRequestMessage request, V2FeedPackage instance, string normalizedVersion)
        {
            if (entry.Id == null && entry.ReadLink == null && entry.EditLink == null)
            {
                return;
            }

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

        private static Uri BuildIdLink(string id, string version, HttpRequestMessage request)
        {
            var packageIdentityQuery = $"(Id='{id}',Version='{version}')";
            var localPath = request.RequestUri.LocalPath
                // Remove closing brackets from Packages collection
                .Replace("/GetUpdates", "/Packages")
                .Replace("/FindPackagesById", "/Packages")
                .Replace("/Search", "/Packages")
                .Replace("/Packages()", "/Packages")
                // Remove package identity query
                .Replace(packageIdentityQuery, string.Empty);

            // Ensure any OData queries remaining are stripped off
            var queryStartIndex = localPath.IndexOf('(');
            if (queryStartIndex != -1)
            {
                localPath = localPath.Substring(0, queryStartIndex);
            }

            return new Uri($"{request.RequestUri.Scheme}://{request.RequestUri.Host}{localPath}{packageIdentityQuery}");
        }
    }
}