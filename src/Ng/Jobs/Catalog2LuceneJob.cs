// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Lucene.Net.Index;
using Microsoft.Extensions.Logging;
using NuGet.Indexing;
using NuGet.Services.Configuration;
using NuGet.Services.Metadata.Catalog;

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
        private string _galleryBaseAddress;
        private TimeSpan? _commitTimeout;
        private Func<HttpMessageHandler> _handlerFunc;
        private string _destination;

        public Catalog2LuceneJob(ITelemetryService telemetryService, ILoggerFactory loggerFactory)
            : base(telemetryService, loggerFactory)
        {
        }

        public override string GetUsage()
        {
            return "Usage: ng catalog2lucene "
                   + $"-{Arguments.Source} <catalog> "
                   + $"[-{Arguments.Registration} <registration-root>. Multiple registration cursors are supported, separated by ';'.] "
                   + $"-{Arguments.LuceneDirectoryType} file|azure "
                   + $"[-{Arguments.LucenePath} <file-path>] "
                   + "|"
                   + $"[-{Arguments.LuceneStorageAccountName} <azure-acc> "
                   + $"-{Arguments.LuceneStorageKeyValue} <azure-key> "
                   + $"-{Arguments.LuceneStorageContainer} <azure-container> "
                   + $"-{Arguments.CommitTimeoutInSeconds} <timeout>"
                   + $"[-{Arguments.VaultName} <keyvault-name> "
                   + $"-{Arguments.ClientId} <keyvault-client-id> "
                   + $"-{Arguments.CertificateThumbprint} <keyvault-certificate-thumbprint> "
                   + $"[-{Arguments.ValidateCertificate} true|false]]] "
                   + $"[-{Arguments.Verbose} true|false] "
                   + $"[-{Arguments.Interval} <seconds>] "
                   + $"[-{Arguments.GalleryBaseAddress} <gallery-base-address>]";
        }

        protected override void Init(IDictionary<string, string> arguments, CancellationToken cancellationToken)
        {
            _directory = CommandHelpers.GetLuceneDirectory(arguments, out var destination);
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
            _galleryBaseAddress = arguments.GetOrDefault<string>(Arguments.GalleryBaseAddress);

            var commitTimeoutInSeconds = arguments.GetOrDefault<int?>(Arguments.CommitTimeoutInSeconds);
            if (commitTimeoutInSeconds.HasValue)
            {
                _commitTimeout = TimeSpan.FromSeconds(commitTimeoutInSeconds.Value);
            }
            else
            {
                _commitTimeout = null;
            }

            Logger.LogInformation("CONFIG source: \"{ConfigSource}\" registration: \"{Registration}\"" +
                                   " catalogBaseAddress: \"{CatalogBaseAddress}\" storageBaseAddress: \"{StorageBaseAddress}\" commitTimeout: \"{CommmitTimeout}\"",
                                   _source,
                                   _registration ?? "(null)",
                                   _catalogBaseAddress ?? "(null)",
                                   _storageBaseAddress ?? "(null)",
                                   _galleryBaseAddress ?? "(null)",
                                   _commitTimeout?.ToString() ?? "(null)");

            _handlerFunc = CommandHelpers.GetHttpMessageHandlerFactory(
                TelemetryService,
                _verbose,
                _catalogBaseAddress,
                _storageBaseAddress);

            _destination = destination;
            TelemetryService.GlobalDimensions[TelemetryConstants.Destination] = _destination;
        }

        protected override async Task RunInternalAsync(CancellationToken cancellationToken)
        {
            using (Logger.BeginScope($"Logging for {{{TelemetryConstants.Destination}}}", _destination))
            using (TelemetryService.TrackDuration(TelemetryConstants.JobLoopSeconds))
            using (var indexWriter = CreateIndexWriter(_directory))
            {
                var collector = new SearchIndexFromCatalogCollector(
                    index: new Uri(_source),
                    indexWriter: indexWriter,
                    commitEachBatch: false,
                    commitTimeout: _commitTimeout,
                    baseAddress: _catalogBaseAddress,
                    galleryBaseAddress: _galleryBaseAddress == null ? null : new Uri(_galleryBaseAddress),
                    telemetryService: TelemetryService,
                    logger: Logger,
                    handlerFunc: _handlerFunc);

                ReadWriteCursor front = new LuceneCursor(indexWriter, MemoryCursor.MinValue);
                var back = _registration == null
                                 ? (ReadCursor)MemoryCursor.CreateMax()
                                 : GetTheLeastAdvancedRegistrationCursor(_registration, cancellationToken);

                bool run;
                do
                {
                    run = await collector.RunAsync(front, back, cancellationToken);

                    await collector.EnsureCommittedAsync(); // commit after each catalog page
                }
                while (run);
            }
        }

        private ReadCursor GetTheLeastAdvancedRegistrationCursor(string registrationArg, CancellationToken cancellationToken)
        {
            string[] registrations = registrationArg.Split(';');

            return new AggregateCursor(registrations.Select(r => new HttpReadCursor(new Uri(r), _handlerFunc)));
        }

        public static IndexWriter CreateIndexWriter(Lucene.Net.Store.Directory directory)
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