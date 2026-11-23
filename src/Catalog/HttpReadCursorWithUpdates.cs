// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;

namespace NuGet.Services.Metadata.Catalog
{
    public class HttpReadCursorWithUpdates : HttpReadCursor
    {
        private readonly ILogger _logger;

        public HttpReadCursorWithUpdates(Uri address, ILogger logger, Func<HttpMessageHandler> handlerFunc = null)
            : base(address, handlerFunc)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public override async Task<string> GetValueInJsonAsync(HttpResponseMessage response)
        {
            var storageDateTimeOffset = response.Headers.Date;
            DateTime? storageDateTimeInUtc = null;
            if (storageDateTimeOffset.HasValue)
            {
                storageDateTimeInUtc = storageDateTimeOffset.Value.UtcDateTime;
            }

            if (!storageDateTimeInUtc.HasValue)
            {
                throw new ArgumentNullException(nameof(storageDateTimeInUtc));
            }

            var content = await response.Content.ReadAsStringAsync();
            var cursorValueWithUpdates = JsonConvert.DeserializeObject<CursorValueWithUpdates>(content, CursorValueWithUpdates.SerializerSettings);

            return JsonConvert.SerializeObject(new { value = GetValue(cursorValueWithUpdates, storageDateTimeInUtc.Value) });
        }

        private string GetValue(CursorValueWithUpdates cursorValueWithUpdates, DateTime storageDateTimeInUtc)
        {
            var minIntervalBeforeToReadUpdate = cursorValueWithUpdates.MinIntervalBeforeToReadUpdate;
            if (minIntervalBeforeToReadUpdate == null || minIntervalBeforeToReadUpdate == TimeSpan.Zero)
            {
                _logger.LogInformation("Read the cursor value: {CursorValue}, at {Address}. MinIntervalBeforeToReadUpdate is not set or set to 0.",
                    cursorValueWithUpdates.Value,
                    _address.AbsoluteUri);

                return cursorValueWithUpdates.Value;
            }

            // If the cursor value was updated before or at storageDateTimeInUtc - minIntervalBeforeToReadUpdate, use the cursor value and there is no need to check the updates.
            if (cursorValueWithUpdates.TimeStamp.HasValue && cursorValueWithUpdates.TimeStamp.Value <= storageDateTimeInUtc - minIntervalBeforeToReadUpdate)
            {
                _logger.LogInformation("Read the cursor value with timeStamp: {TimeStamp} and value: {CursorValue}, at {Address}. " +
                    "(Storage DateTime: {StorageDateTime}, MinIntervalBeforeToReadUpdate: {MinIntervalBeforeToReadUpdate})",
                    cursorValueWithUpdates.TimeStamp.Value.ToString(CursorValueWithUpdates.SerializerSettings.DateFormatString),
                    cursorValueWithUpdates.Value,
                    _address.AbsoluteUri,
                    storageDateTimeInUtc.ToString(CursorValueWithUpdates.SerializerSettings.DateFormatString),
                    minIntervalBeforeToReadUpdate);

                return cursorValueWithUpdates.Value;
            }

            var updates = cursorValueWithUpdates.Updates.OrderByDescending(u => u.TimeStamp).ToList();
            foreach (var update in updates)
            {
                if (update.TimeStamp <= storageDateTimeInUtc - minIntervalBeforeToReadUpdate)
                {
                    _logger.LogInformation("Read the cursor update with timeStamp: {TimeStamp} and value: {UpdateValue}, at {Address}. " +
                        "(Storage DateTime: {StorageDateTime}, MinIntervalBeforeToReadUpdate: {MinIntervalBeforeToReadUpdate})",
                        update.TimeStamp.ToString(CursorValueWithUpdates.SerializerSettings.DateFormatString),
                        update.Value,
                        _address.AbsoluteUri,
                        storageDateTimeInUtc.ToString(CursorValueWithUpdates.SerializerSettings.DateFormatString),
                        minIntervalBeforeToReadUpdate);

                    return update.Value;
                }
            }

            if (updates.Count > 0)
            {
                _logger.LogWarning("Unable to find the cursor update and the oldest cursor update has timeStamp: {TimeStamp}, at {Address}. " +
                    "(Storage DateTime: {StorageDateTime}, MinIntervalBeforeToReadUpdate: {MinIntervalBeforeToReadUpdate})",
                    updates.Last().TimeStamp.ToString(CursorValueWithUpdates.SerializerSettings.DateFormatString),
                    _address.AbsoluteUri,
                    storageDateTimeInUtc.ToString(CursorValueWithUpdates.SerializerSettings.DateFormatString),
                    minIntervalBeforeToReadUpdate);
            }
            else
            {
                _logger.LogWarning("Unable to find the cursor update and the count of updates is {CursorUpdatesCount}, at {Address}.",
                    updates.Count,
                    _address.AbsoluteUri);
            }

            throw new InvalidOperationException("Unable to find the cursor update.");
        }
    }
}
