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
        public Dictionary<Uri, StorageContent> Content { get; }

        public Dictionary<Uri, StorageListItem> ListMock { get; }

        public MemoryStorage()
            : this(new Uri("http://tempuri.org"))
        {
        }

        public MemoryStorage(Uri baseAddress)
          : base(baseAddress)
        {
            Content = new Dictionary<Uri, StorageContent>();
            ListMock = new Dictionary<Uri, StorageListItem>();
        }

        private MemoryStorage(Uri baseAddress, Dictionary<Uri, StorageContent> content)
          : base(baseAddress)
        {
            Content = content;
        }

        public Storage WithName(string name)
        {
            return new MemoryStorage(new Uri(BaseAddress + name), Content);
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

        public override Task<IEnumerable<StorageListItem>> List(CancellationToken cancellationToken)
        {
            return Task.FromResult(Content.Keys.AsEnumerable().Select(x => 
                ListMock.ContainsKey(x) ? ListMock[x] : new StorageListItem(x, DateTime.UtcNow)));
        }
    }
}