// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.
using Newtonsoft.Json.Linq;
using NuGet.Services.Metadata.Catalog.Persistence;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace NuGet.Services.Metadata.Catalog
{
    public class DurableCursor : ReadWriteCursor
    {
        Uri _address;
        Storage _storage;
        DateTime _defaultValue;

        public DurableCursor(Uri address, Storage storage, DateTime defaultValue)
        {
            _address = address;
            _storage = storage;
            _defaultValue = defaultValue;
        }

        public override async Task Save(CancellationToken cancellationToken)
        {
            JObject obj = new JObject { { "value", Value.ToString("O") } };
            StorageContent content = new StringStorageContent(obj.ToString(), "application/json", "no-store");
            await _storage.Save(_address, content, cancellationToken);
        }

        public override async Task Load(CancellationToken cancellationToken)
        {
            string json = await _storage.LoadString(_address, cancellationToken);

            if (json == null)
            {
                Value = _defaultValue;
                return;
            }

            JObject obj = JObject.Parse(json);
            Value = obj["value"].ToObject<DateTime>();
        }
    }
}
