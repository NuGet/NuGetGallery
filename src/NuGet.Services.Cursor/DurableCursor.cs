// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using NuGet.Services.Storage;

namespace NuGet.Services.Cursor
{
    public class DurableCursor : ReadWriteCursor<DateTimeOffset>
    {
        Uri _address;
        Storage.Storage _storage;
        DateTimeOffset _defaultValue;

        public DurableCursor(Uri address, Storage.Storage storage, DateTimeOffset defaultValue)
        {
            _address = address ?? throw new ArgumentNullException(nameof(address));
            _storage = storage ?? throw new ArgumentNullException(nameof(storage));
            _defaultValue = defaultValue;
        }

        public override async Task Save(CancellationToken cancellationToken)
        {
            JObject obj = new JObject { { "value", Value.ToString("O") } };
            StorageContent content = new StringStorageContent(obj.ToString(), "application/json", "no-store");
            await _storage.Save(_address, content, overwrite: true, cancellationToken: cancellationToken);
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
            Value = obj["value"].ToObject<DateTimeOffset>();
        }
    }
}
