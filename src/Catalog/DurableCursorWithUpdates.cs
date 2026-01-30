// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using NuGet.Services.Metadata.Catalog.Persistence;

namespace NuGet.Services.Metadata.Catalog
{
    public class DurableCursorWithUpdates : DurableCursor
    {
        private readonly int _maxNumberOfUpdatesToKeep;
        private readonly TimeSpan _minIntervalBetweenTwoUpdates;

        public DurableCursorWithUpdates(Uri address, Persistence.Storage storage, DateTime defaultValue,
            int maxNumberOfUpdatesToKeep, TimeSpan minIntervalBetweenTwoUpdates)
            : base(address, storage, defaultValue)
        {
            if (maxNumberOfUpdatesToKeep < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(maxNumberOfUpdatesToKeep), "maxNumberOfUpdatesToKeep must be equal or larger than 0.");
            }

            if (minIntervalBetweenTwoUpdates < TimeSpan.Zero)
            {
                throw new ArgumentOutOfRangeException(nameof(minIntervalBetweenTwoUpdates), "minIntervalBetweenTwoUpdates must be equal or larger than 0.");
            }

            _maxNumberOfUpdatesToKeep = maxNumberOfUpdatesToKeep;
            _minIntervalBetweenTwoUpdates = minIntervalBetweenTwoUpdates;
        }

        public override async Task SaveAsync(CancellationToken cancellationToken)
        {
            var cursorValueWithUpdates = new CursorValueWithUpdates();

            var storageContent = await _storage.LoadStringStorageContentAsync(_address, cancellationToken);
            if (storageContent != null && storageContent.Content != null)
            {
                cursorValueWithUpdates = JsonConvert.DeserializeObject<CursorValueWithUpdates>(storageContent.Content, new JsonSerializerSettings { DateFormatString = "O" });
            }

            cursorValueWithUpdates.Value = Value.ToString("O");
            if (storageContent != null)
            {
                cursorValueWithUpdates.Updates = GetUpdates(cursorValueWithUpdates, storageContent.StorageDateTimeInUtc);
            }

            var content = new StringStorageContent(JsonConvert.SerializeObject(cursorValueWithUpdates, new JsonSerializerSettings { DateFormatString = "O" }),
                "application/json", Constants.NoStoreCacheControl);

            await _storage.SaveAsync(_address, content, cancellationToken);
        }

        private IList<CursorValueUpdate> GetUpdates(CursorValueWithUpdates cursorValueWithUpdates, DateTime? storageDateTimeInUtc)
        {
            if (!storageDateTimeInUtc.HasValue)
            {
                throw new ArgumentNullException(nameof(storageDateTimeInUtc));
            }

            var updates = cursorValueWithUpdates.Updates.OrderByDescending(u => u.UpdateTimeStamp).ToList();
            if (updates.Count == 0 || (updates.Count > 0 && (storageDateTimeInUtc.Value - updates.First().UpdateTimeStamp >= _minIntervalBetweenTwoUpdates)))
            {
                updates.Insert(0, new CursorValueUpdate(storageDateTimeInUtc.Value, Value.ToString("O")));
            }

            while (updates.Count > _maxNumberOfUpdatesToKeep)
            {
                updates.RemoveAt(updates.Count - 1);
            }

            return updates;
        }
    }
}
