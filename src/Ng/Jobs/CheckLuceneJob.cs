// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Lucene.Net.Index;

namespace Ng.Jobs
{
    public class CheckLuceneJob : NgJob
    {
        private Lucene.Net.Store.Directory _directory;

        public CheckLuceneJob(ILoggerFactory loggerFactory) : base(loggerFactory)
        {
        }

        public override string GetUsage()
        {
            return "Usage: ng checklucene "
                   + $"-{Arguments.LuceneDirectoryType} file|azure "
                   + $"[-{Arguments.LucenePath} <file-path>]"
                   + $"|"
                   + $"[-{Arguments.LuceneStorageAccountName} <azure-acc> "
                   + $"-{Arguments.LuceneStorageKeyValue} <azure-key> "
                   + $"-{Arguments.LuceneStorageContainer} <azure-container>]";
        }

        protected override void Init(IDictionary<string, string> arguments, CancellationToken cancellationToken)
        {
            _directory = CommandHelpers.GetLuceneDirectory(arguments);
        }
        
        protected override Task RunInternal(CancellationToken cancellationToken)
        {
            using (var reader = IndexReader.Open(_directory, true))
            {
                Logger.LogInformation("Lucene index contains: {numDocs} documents", reader.NumDocs());

                var commitUserData = reader.CommitUserData;

                if (commitUserData == null)
                {
                    Logger.LogWarning("commitUserData is null");
                }
                else
                {
                    Logger.LogInformation("commitUserData:");
                    foreach (var entry in commitUserData)
                    {
                        Logger.LogInformation("  {EntryKey} = {EntryValue}", entry.Key, entry.Value);
                    }
                }
            }

            return Task.FromResult(false);
        }
    }
}
