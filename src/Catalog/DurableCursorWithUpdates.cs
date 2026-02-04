// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using NuGet.Services.Metadata.Catalog.Persistence;

namespace NuGet.Services.Metadata.Catalog
{
    public class DurableCursorWithUpdates : DurableCursor
    {
        private readonly ILogger _logger;

        private readonly int _maxNumberOfUpdatesToKeep;
        private readonly TimeSpan _minIntervalBetweenTwoUpdates;

        public DurableCursorWithUpdates(Uri address, Persistence.Storage storage, DateTime defaultValue, ILogger logger,
            int maxNumberOfUpdatesToKeep, TimeSpan minIntervalBetweenTwoUpdates)
            : base(address, storage, defaultValue)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));

            if (maxNumberOfUpdatesToKeep < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(maxNumberOfUpdatesToKeep), $"{nameof(maxNumberOfUpdatesToKeep)} must be equal or larger than 0.");
            }

            if (minIntervalBetweenTwoUpdates < TimeSpan.Zero)
            {
                throw new ArgumentOutOfRangeException(nameof(minIntervalBetweenTwoUpdates), $"{nameof(minIntervalBetweenTwoUpdates)} must be equal or larger than 0.");
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
                cursorValueWithUpdates = JsonConvert.DeserializeObject<CursorValueWithUpdates>(storageContent.Content, CursorValueWithUpdates.SerializerSettings);

                _logger.LogInformation("Read the cursor value: {CursorValue} with {CursorUpdatesCount}, at {Address}.", cursorValueWithUpdates.Value,
                    cursorValueWithUpdates.Updates.Count + (cursorValueWithUpdates.Updates.Count <= 1 ? " update" : " updates"), _address.AbsoluteUri);
            }

            cursorValueWithUpdates.Value = Value.ToString("O");
            if (storageContent != null)
            {
                cursorValueWithUpdates.Updates = GetUpdates(cursorValueWithUpdates, storageContent.StorageDateTimeInUtc);
            }

            var content = new StringStorageContent(JsonConvert.SerializeObject(cursorValueWithUpdates, CursorValueWithUpdates.SerializerSettings),
                "application/json", Constants.NoStoreCacheControl);

            await _storage.SaveAsync(_address, content, cancellationToken);

            _logger.LogInformation("Updated the cursor value: {CursorValue} with {CursorUpdatesCount}, at {Address}.", cursorValueWithUpdates.Value,
                cursorValueWithUpdates.Updates.Count + (cursorValueWithUpdates.Updates.Count <= 1 ? " update" : " updates"), _address.AbsoluteUri);
        }

        private IList<CursorValueUpdate> GetUpdates(CursorValueWithUpdates cursorValueWithUpdates, DateTime? storageDateTimeInUtc)
        {
            if (!storageDateTimeInUtc.HasValue)
            {
                throw new ArgumentNullException(nameof(storageDateTimeInUtc));
            }

            var updates = cursorValueWithUpdates.Updates.OrderByDescending(u => u.TimeStamp).ToList();
            if (updates.Count == 0 || (updates.Count > 0 && (storageDateTimeInUtc.Value - updates.First().TimeStamp >= _minIntervalBetweenTwoUpdates)))
            {
                var update = new CursorValueUpdate(storageDateTimeInUtc.Value, cursorValueWithUpdates.Value);
                updates.Insert(0, update);

                _logger.LogInformation("Added the cursor update with TimeStamp: {TimeStamp} and value: {UpdateValue}.",
                    update.TimeStamp.ToString(CursorValueWithUpdates.SerializerSettings.DateFormatString), update.Value);
            }

            while (updates.Count > 0 && updates.Count > _maxNumberOfUpdatesToKeep)
            {
                var update = updates[updates.Count - 1];

                _logger.LogInformation("Deleted the cursor update with TimeStamp: {TimeStamp} and value: {UpdateValue}.",
                    update.TimeStamp.ToString(CursorValueWithUpdates.SerializerSettings.DateFormatString), update.Value);

                updates.RemoveAt(updates.Count - 1);
            }

            return updates;
        }
    }
}
