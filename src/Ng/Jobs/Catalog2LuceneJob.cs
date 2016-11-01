// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Threading;
using System.Net.Http;
using NuGet.Services.Metadata.Catalog;
using Lucene.Net.Index;
using NuGet.Indexing;
using NuGet.Services.Configuration;

namespace Ng.Jobs
{
    public class Catalog2LuceneJob : LoopingNgJob
    {
        private bool _verbose;
        private string _source;
        private string _registration;
        private Lucene.Net.Store.Directory _directory;
        private string _catalogBaseAddress;
        private string _storageBaseAddress;
        private Func<HttpMessageHandler> _handlerFunc;

        public Catalog2LuceneJob(ILoggerFactory loggerFactory)
            : base(loggerFactory)
        {
        }

        public override string GetUsage()
        {
            return "Usage: ng catalog2lucene "
                   + $"-{Arguments.Source} <catalog> "
                   + $"[-{Arguments.Registration} <registration-root>] "
                   + $"-{Arguments.LuceneDirectoryType} file|azure "
                   + $"[-{Arguments.LucenePath} <file-path>] "
                   + "|"
                   + $"[-{Arguments.LuceneStorageAccountName} <azure-acc> "
                   + $"-{Arguments.LuceneStorageKeyValue} <azure-key> "
                   + $"-{Arguments.LuceneStorageContainer} <azure-container> "
                   + $"[-{Arguments.VaultName} <keyvault-name> "
                   + $"-{Arguments.ClientId} <keyvault-client-id> "
                   + $"-{Arguments.CertificateThumbprint} <keyvault-certificate-thumbprint> "
                   + $"[-{Arguments.ValidateCertificate} true|false]]] "
                   + $"[-{Arguments.Verbose} true|false] "
                   + $"[-{Arguments.Interval} <seconds>]";
        }

        protected override void Init(IDictionary<string, string> arguments, CancellationToken cancellationToken)
        {
            _directory = CommandHelpers.GetLuceneDirectory(arguments);
            _source = arguments.GetOrThrow<string>(Arguments.Source);
            _verbose = arguments.GetOrDefault(Arguments.Verbose, false);

            _registration = arguments.GetOrDefault<string>(Arguments.Registration);
            if (_registration == null)
            {
                Logger.LogInformation("Lucene index will be created up to the end of the catalog (alternatively if you provide a registration it will not pass that)");
            }

            _catalogBaseAddress = arguments.GetOrDefault<string>(Arguments.CatalogBaseAddress);
            if (_catalogBaseAddress == null)
            {
                Logger.LogInformation("No catalogBaseAddress was specified so the Lucene index will NOT contain the storage paths");
            }

            _storageBaseAddress = arguments.GetOrDefault<string>(Arguments.StorageBaseAddress);

            Logger.LogInformation("CONFIG source: \"{ConfigSource}\" registration: \"{Registration}\"" +
                                   " catalogBaseAddress: \"{CatalogBaseAddress}\" storageBaseAddress: \"{StorageBaseAddress}\"",
                                   _source,
                                   _registration ?? "(null)",
                                   _catalogBaseAddress ?? "(null)",
                                   _storageBaseAddress ?? "(null)");

            _handlerFunc = CommandHelpers.GetHttpMessageHandlerFactory(_verbose, _catalogBaseAddress, _storageBaseAddress);
        }

        protected override async Task RunInternal(CancellationToken cancellationToken)
        {
            using (var indexWriter = CreateIndexWriter(_directory))
            {
                var collector = new SearchIndexFromCatalogCollector(
                    Logger,
                    index: new Uri(_source),
                    indexWriter: indexWriter,
                    commitEachBatch: false,
                    baseAddress: _catalogBaseAddress,
                    handlerFunc: _handlerFunc);

                ReadWriteCursor front = new LuceneCursor(indexWriter, MemoryCursor.MinValue);

                var back = _registration == null
                    ? (ReadCursor)MemoryCursor.CreateMax()
                    : new HttpReadCursor(new Uri(_registration), _handlerFunc);

                bool run;
                do
                {
                    run = await collector.Run(front, back, cancellationToken);

                    collector.EnsureCommitted(); // commit after each catalog page
                }
                while (run);
            }
        }

        private static IndexWriter CreateIndexWriter(Lucene.Net.Store.Directory directory)
        {
            var create = !IndexReader.IndexExists(directory);

            directory.EnsureOpen();

            if (!create)
            {
                if (IndexWriter.IsLocked(directory))
                {
                    IndexWriter.Unlock(directory);
                }
            }

            var indexWriter = new IndexWriter(directory, new PackageAnalyzer(), create, IndexWriter.MaxFieldLength.UNLIMITED);

            NuGetMergePolicyApplyer.ApplyTo(indexWriter);

            indexWriter.SetSimilarity(new CustomSimilarity());

            return indexWriter;
        }
    }
}
