// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using Microsoft.Data.OData;
using Microsoft.Data.OData.Atom;
using System;
using System.Net.Http;

namespace NuGetGallery.OData.Serializers
{
    internal class V1FeedPackageAnnotationStrategy
        : FeedPackageAnnotationStrategy<V1FeedPackage>
    {
        public V1FeedPackageAnnotationStrategy(string contentType)
            : base(contentType)
        {
        }

        public override void Annotate(HttpRequestMessage request, ODataEntry entry, object entityInstance)
        {
            var feedPackage = entityInstance as V1FeedPackage;
            if (feedPackage == null)
            {
                return;
            }

            // Set Atom entry metadata
            var atomEntryMetadata = new AtomEntryMetadata();
            atomEntryMetadata.Title = feedPackage.Title;

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
                ReadLink = BuildLinkForStreamProperty("v1", feedPackage.Id, feedPackage.Version, request)
            };
        }
    }
}