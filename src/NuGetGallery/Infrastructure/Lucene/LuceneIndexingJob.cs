// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Data;
using System.Data.SqlClient;
using System.Threading.Tasks;
using WebBackgrounder;

namespace NuGetGallery
{
    public class LuceneIndexingJob : Job
    {
        private readonly LuceneIndexingService _indexingService;

        public static async Task<LuceneIndexingJob> Create(TimeSpan frequence, TimeSpan timeout, LuceneIndexingService indexingService)
        {
            var job = new LuceneIndexingJob(frequence, timeout, indexingService);

            // Updates the index synchronously first time job is created.
            // For startup code resiliency, we should handle exceptions for the database being down.
            try
            {
                await job.Execute();
            }
            catch (SqlException e)
            {
                QuietLog.LogHandledException(e);
            }
            catch (DataException e)
            {
                QuietLog.LogHandledException(e);
            }

            return job;
        }

        private LuceneIndexingJob(TimeSpan frequence, TimeSpan timeout, LuceneIndexingService indexingService)
            : base("Lucene", frequence, timeout)
        {
            _indexingService = indexingService;
        }

        public override Task Execute()
        {
            return _indexingService.UpdateIndex();
        }
    }
}
