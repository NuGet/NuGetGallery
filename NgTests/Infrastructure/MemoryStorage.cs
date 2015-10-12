// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using NuGet.Services.Metadata.Catalog.Persistence;

namespace NgTests.Infrastructure
{
    public class MemoryStorage 
        : Storage
    {
        public Dictionary<Uri, StorageContent> Content { get; private set;  }

        public MemoryStorage()
            : this(new Uri("http://tempuri.org"))
        {
        }

        public MemoryStorage(Uri baseAddress)
          : base(baseAddress)
        {
            Content = new Dictionary<Uri, StorageContent>();
        }

        protected override Task OnSave(Uri resourceUri, StorageContent content, CancellationToken cancellationToken)
        {
            Content[resourceUri] = content;
            return Task.FromResult(true);
        }

        protected override Task<StorageContent> OnLoad(Uri resourceUri, CancellationToken cancellationToken)
        {
            StorageContent content;
            Content.TryGetValue(resourceUri, out content);
            return Task.FromResult(content);
        }

        protected override Task OnDelete(Uri resourceUri, CancellationToken cancellationToken)
        {
            if (Content.ContainsKey(resourceUri))
            {
                Content.Remove(resourceUri);
            }
            return Task.FromResult(true);
        }
        
        public override bool Exists(string fileName)
        {
            return Content.Keys.Any(k => k.PathAndQuery.EndsWith(fileName));
        }

        public override Task<IEnumerable<Uri>> List(CancellationToken cancellationToken)
        {
            return Task.FromResult(Content.Keys.AsEnumerable());
        }
    }
}