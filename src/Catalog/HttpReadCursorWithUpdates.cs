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
        private readonly TimeSpan _minIntervalBeforeToReadCursorUpdateValue;

        public HttpReadCursorWithUpdates(TimeSpan minIntervalBeforeToReadCursorUpdateValue, Uri address, ILogger logger,
            Func<HttpMessageHandler> handlerFunc = null)
            : base(address, handlerFunc)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _minIntervalBeforeToReadCursorUpdateValue = minIntervalBeforeToReadCursorUpdateValue;
        }

        public override async Task<string> GetValueInJsonAsync(HttpResponseMessage response)
        {
            var storageDateTimeOffset = response.Headers.Date;
            DateTime? storageDateTimeInUtc = null;
            if (storageDateTimeOffset.HasValue)
            {
                storageDateTimeInUtc = storageDateTimeOffset.Value.UtcDateTime;
            }

            var update = await GetUpdate(response, storageDateTimeInUtc);

            return $"{{\"value\": \"{update.Value}\"}}";
        }

        private async Task<CursorValueUpdate> GetUpdate(HttpResponseMessage response, DateTime? storageDateTimeInUtc)
        {
            if (!storageDateTimeInUtc.HasValue)
            {
                throw new ArgumentNullException(nameof(storageDateTimeInUtc));
            }

            var content = await response.Content.ReadAsStringAsync();
            var cursorValueWithUpdates = JsonConvert.DeserializeObject<CursorValueWithUpdates>(content, CursorValueWithUpdates.SerializerSettings);
            var updates = cursorValueWithUpdates.Updates.OrderByDescending(u => u.TimeStamp).ToList();

            foreach (var update in updates)
            {
                if (update.TimeStamp <= storageDateTimeInUtc.Value - _minIntervalBeforeToReadCursorUpdateValue)
                {
                    _logger.LogInformation("Read the cursor update with timeStamp: {TimeStamp} and value: {UpdateValue}, at {Address}. " +
                        "(Storage DateTime: {StorageDateTime}, MinIntervalBeforeToReadCursorUpdateValue: {MinIntervalBeforeToReadCursorUpdateValue})",
                        update.TimeStamp.ToString(CursorValueWithUpdates.SerializerSettings.DateFormatString),
                        update.Value,
                        _address.AbsoluteUri,
                        storageDateTimeInUtc.Value.ToString(CursorValueWithUpdates.SerializerSettings.DateFormatString),
                        _minIntervalBeforeToReadCursorUpdateValue);

                    return update;
                }
            }

            if (updates.Count > 0)
            {
                _logger.LogWarning("Unable to find the cursor update and the oldest cursor update has timeStamp: {TimeStamp}, at {Address}. " +
                    "(Storage DateTime: {StorageDateTime}, MinIntervalBeforeToReadCursorUpdateValue: {MinIntervalBeforeToReadCursorUpdateValue})",
                    updates.Last().TimeStamp.ToString(CursorValueWithUpdates.SerializerSettings.DateFormatString),
                    _address.AbsoluteUri,
                    storageDateTimeInUtc.Value.ToString(CursorValueWithUpdates.SerializerSettings.DateFormatString),
                    _minIntervalBeforeToReadCursorUpdateValue);
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
