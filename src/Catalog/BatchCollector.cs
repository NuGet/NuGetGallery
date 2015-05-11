// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;

namespace NuGet.Services.Metadata.Catalog
{
    public abstract class BatchCollector : CollectorBase
    {
        int _batchSize;

        public BatchCollector(Uri index, Func<HttpMessageHandler> handlerFunc = null, int batchSize = 200)
            : base(index, handlerFunc)
        {
            _batchSize = batchSize;
            BatchCount = 0;
            PreviousRunBatchCount = 0;
        }

        public int BatchCount { get; private set; }

        public int PreviousRunBatchCount { get; private set; }

        protected override async Task<bool> Fetch(CollectorHttpClient client, ReadWriteCursor front, ReadCursor back)
        {
            int beforeBatchCount = BatchCount;

            IList<JObject> items = new List<JObject>();

            JObject root = await client.GetJObjectAsync(Index);

            JToken context = null;
            root.TryGetValue("@context", out context);

            IEnumerable<JToken> rootItems = root["items"].OrderBy(item => item["commitTimeStamp"].ToObject<DateTime>());

            DateTime resumeDateTime = front.Value;

            bool acceptNextBatch = true;

            foreach (JObject rootItem in rootItems)
            {
                if (!acceptNextBatch)
                {
                    break;
                }

                DateTime rootItemCommitTimeStamp = rootItem["commitTimeStamp"].ToObject<DateTime>();

                if (rootItemCommitTimeStamp <= front.Value)
                {
                    continue;
                }

                Uri pageUri = rootItem["@id"].ToObject<Uri>();
                JObject page = await client.GetJObjectAsync(pageUri);

                IEnumerable<JToken> pageItems = page["items"].OrderBy(item => item["commitTimeStamp"].ToObject<DateTime>());

                foreach (JObject pageItem in pageItems)
                {
                    DateTime pageItemCommitTimeStamp = pageItem["commitTimeStamp"].ToObject<DateTime>();

                    if (pageItemCommitTimeStamp <= front.Value)
                    {
                        continue;
                    }

                    if (pageItemCommitTimeStamp > back.Value)
                    {
                        break;
                    }

                    items.Add(pageItem);
                    resumeDateTime = pageItemCommitTimeStamp;

                    if (items.Count == _batchSize)
                    {
                        acceptNextBatch = await ProcessBatch(client, items, context, front, resumeDateTime);

                        if (!acceptNextBatch)
                        {
                            break;
                        }
                    }
                }
            }

            if (acceptNextBatch && items.Count > 0)
            {
                await ProcessBatch(client, items, context, front, resumeDateTime);
            }

            int afterBatchCount = BatchCount;

            PreviousRunBatchCount = (afterBatchCount - beforeBatchCount);

            return (PreviousRunBatchCount > 0);
        }

        async Task<bool> ProcessBatch(CollectorHttpClient client, IList<JObject> items, JToken context, ReadWriteCursor front, DateTime resumeDateTime)
        {
            bool acceptNextBatch = await OnProcessBatch(client, items, (JObject)context, resumeDateTime);
            BatchCount++;
            items.Clear();

            front.Value = resumeDateTime;
            await front.Save();

            return acceptNextBatch;
        }

        protected virtual Task<bool> OnProcessBatch(CollectorHttpClient client, IList<JObject> items, JObject context, DateTime resumeDateTime)
        {
            return OnProcessBatch(client, items, context);
        }

        protected abstract Task<bool> OnProcessBatch(CollectorHttpClient client, IList<JObject> items, JObject context);
    }
}
