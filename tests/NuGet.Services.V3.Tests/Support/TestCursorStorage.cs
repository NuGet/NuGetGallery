// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using NuGet.Services.Metadata.Catalog.Persistence;

namespace NuGet.Services
{
    public class TestCursorStorage : Metadata.Catalog.Persistence.Storage
    {
        public DateTime CursorValue { get; set; }

        public TestCursorStorage(Uri baseAddress) : base(baseAddress)
        {
        }

        protected override async Task<StorageContent> OnLoadAsync(
            Uri resourceUri,
            CancellationToken cancellationToken)
        {
            await Task.Yield();

            return new StringStorageContent(JsonConvert.SerializeObject(new Cursor
            {
                Value = CursorValue,
            }));
        }

        protected override Task OnSaveAsync(
            Uri resourceUri,
            StorageContent content,
            CancellationToken cancellationToken)
        {
            using (var stream = content.GetContentStream())
            using (var reader = new StreamReader(stream))
            {
                var json = reader.ReadToEnd();
                var cursor = JsonConvert.DeserializeObject<Cursor>(json);
                CursorValue = cursor.Value;
            }

            return Task.CompletedTask;
        }

        public override bool Exists(
            string fileName) => throw new NotImplementedException();
        public override Task<IEnumerable<StorageListItem>> ListAsync(
            CancellationToken cancellationToken) => throw new NotImplementedException();
        protected override Task OnCopyAsync(
            Uri sourceUri,
            IStorage destinationStorage,
            Uri destinationUri,
            IReadOnlyDictionary<string, string> destinationProperties,
            CancellationToken cancellationToken) => throw new NotImplementedException();
        protected override Task OnDeleteAsync(
            Uri resourceUri, 
            DeleteRequestOptions deleteRequestOptions,
            CancellationToken cancellationToken) => throw new NotImplementedException();
    }
}
