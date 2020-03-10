// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Net.Http;
using Microsoft.Extensions.Options;
using NuGet.Services.Metadata.Catalog;
using NuGet.Services.Metadata.Catalog.Persistence;

namespace NuGet.Jobs.RegistrationComparer
{
    public static class CursorUtility
    {
        public static Dictionary<string, ReadCursor> GetRegistrationCursors(
            Func<HttpMessageHandler> handlerFunc,
            IOptionsSnapshot<RegistrationComparerConfiguration> options)
        {
            var hiveCursors = new Dictionary<string, ReadCursor>();
            foreach (var hives in options.Value.Registrations)
            {
                var cursorUrl = new Uri(hives.Legacy.StorageBaseUrl.TrimEnd('/') + "/cursor.json");
                hiveCursors.Add(cursorUrl.AbsoluteUri, new HttpReadCursor(cursorUrl, DateTime.MinValue, handlerFunc));
            }

            return hiveCursors;
        }

        public static KeyValuePair<string, DurableCursor> GetComparerCursor(IStorageFactory storageFactory)
        {
            return GetDurableCursor(storageFactory, "comparer-cursor.json");
        }

        private static KeyValuePair<string, DurableCursor> GetDurableCursor(IStorageFactory storageFactory, string name)
        {
            var cursorStorage = storageFactory.Create();
            var cursorUri = cursorStorage.ResolveUri(name);
            return new KeyValuePair<string, DurableCursor>(
                cursorUri.AbsoluteUri,
                new DurableCursor(cursorUri, cursorStorage, DateTime.MinValue));
        }
    }
}
