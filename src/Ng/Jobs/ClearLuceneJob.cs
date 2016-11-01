// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Lucene.Net.Index;
using Lucene.Net.Analysis.Standard;

namespace Ng.Jobs
{
    public class ClearLuceneJob : NgJob
    {
        private Lucene.Net.Store.Directory _directory;

        public ClearLuceneJob(ILoggerFactory loggerFactory) : base(loggerFactory)
        {
        }

        public override string GetUsage()
        {
            return "Usage: ng clearlucene "
                   + $"-{Arguments.LuceneDirectoryType} file|azure "
                   + $"[-{Arguments.LucenePath} <file-path>]"
                   + "|"
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
            if (IndexReader.IndexExists(_directory))
            {
                using (var writer = new IndexWriter(_directory, new StandardAnalyzer(Lucene.Net.Util.Version.LUCENE_30), true, IndexWriter.MaxFieldLength.UNLIMITED))
                {
                    writer.DeleteAll();
                    writer.Commit(new Dictionary<string, string>());
                }
            }

            Logger.LogInformation("All Done");

            return Task.FromResult(false);
        }
    }
}
