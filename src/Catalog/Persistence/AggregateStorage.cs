// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace NuGet.Services.Metadata.Catalog.Persistence
{
    public class AggregateStorage : Storage
    {
        private readonly Storage _primaryStorage;
        private readonly Storage[] _secondaryStorage;

        public AggregateStorage(Uri baseAddress, Storage primaryStorage, Storage[] secondaryStorage)
            : base(baseAddress)
        {
            _primaryStorage = primaryStorage;
            _secondaryStorage = secondaryStorage;

            BaseAddress = _primaryStorage.BaseAddress;
        }
        
        protected override Task OnSave(Uri resourceUri, StorageContent content, CancellationToken cancellationToken)
        {
            var tasks = new List<Task>();
            tasks.Add(_primaryStorage.Save(resourceUri, content, cancellationToken));

            foreach (var storage in _secondaryStorage)
            {
                var secondaryResourceUri = resourceUri.ToString()
                    .Replace(_primaryStorage.BaseAddress.ToString(), storage.BaseAddress.ToString());

                // todo replace URL in resource URI + content
                tasks.Add(storage.Save(new Uri(secondaryResourceUri), content, cancellationToken));
            }

            return Task.WhenAll(tasks);
        }

        protected override Task<StorageContent> OnLoad(Uri resourceUri, CancellationToken cancellationToken)
        {
            return _primaryStorage.Load(resourceUri, cancellationToken);
        }

        protected override Task OnDelete(Uri resourceUri, CancellationToken cancellationToken)
        {
            var tasks = new List<Task>();
            tasks.Add(_primaryStorage.Delete(resourceUri, cancellationToken));

            foreach (var storage in _secondaryStorage)
            {
                var secondaryResourceUri = resourceUri.ToString()
                    .Replace(_primaryStorage.BaseAddress.ToString(), storage.BaseAddress.ToString());
                
                // todo replace URL in resource URI
                tasks.Add(storage.Delete(new Uri(secondaryResourceUri), cancellationToken));
            }

            return Task.WhenAll(tasks);
        }

        public override bool Exists(string fileName)
        {
            return _primaryStorage.Exists(fileName);
        }

        public override Task<IEnumerable<Uri>> List(CancellationToken cancellationToken)
        {
            return _primaryStorage.List(cancellationToken);
        }
    }
}