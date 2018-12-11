// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.IO;
using NuGetGallery;

namespace NuGet.Services.AzureSearch.Catalog2AzureSearch.Integration
{
    public class InMemoryFileReference : IFileReference
    {
        private readonly byte[] _bytes;

        public InMemoryFileReference(string contentId, byte[] bytes)
        {
            ContentId = contentId;
            _bytes = bytes;
        }

        public string ContentId { get; }

        public Stream OpenRead()
        {
            return new MemoryStream(_bytes);
        }

        public string AsString
        {
            get
            {
                using (var stream = OpenRead())
                using (var reader = new StreamReader(stream))
                {
                    return reader.ReadToEnd();
                }
            }
        }
    }
}
