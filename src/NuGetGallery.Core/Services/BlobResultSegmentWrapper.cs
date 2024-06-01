// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;
using System.Linq;
using Azure.Storage.Blobs.Models;

namespace NuGetGallery
{
    public class BlobResultSegmentWrapper : ISimpleBlobResultSegment
    {
        public BlobResultSegmentWrapper(IReadOnlyList<BlobItem> items, string continuationToken)
        {
            // For now, assume all of the blobs are block blobs. This library's storage abstraction only allows
            // creation of block blobs so it's good enough for now. If another caller created a non-block blob, this
            // cast will fail at runtime.
            Results = segment.Results.Cast<CloudBlockBlob>().Select(x => new CloudBlobWrapper(x)).ToList();
            ContinuationToken = new BlobListContinuationToken(continuationToken);
        }

        public IReadOnlyList<ISimpleCloudBlob> Results { get; }
        public IBlobListContinuationToken ContinuationToken { get; }

        private static ISimpleCloudBlob ConvertListItem(BlobItem item)
        {
            
        }
    }
}
