// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Autofac;
using Autofac.Extensions.DependencyInjection;
using Microsoft.ApplicationInsights;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NuGet.Jobs.Catalog2Registration;
using NuGet.Packaging.Core;
using NuGet.Protocol.Catalog;
using NuGet.Services;
using NuGet.Services.Configuration;
using NuGet.Services.Metadata.Catalog;
using NuGet.Services.Metadata.Catalog.Helpers;
using NuGet.Services.Metadata.Catalog.Registration;
using VDS.RDF;

namespace Ng.Jobs
{
    public class LightningJob : NgJob
    {
        private const string GraphDriver = "graph";
        private const string JsonDriver = "json";

        public LightningJob(ITelemetryService telemetryService, ILoggerFactory loggerFactory)
            : base(telemetryService, loggerFactory)
        {
        }

        private static void PrintLightning()
        {
            var currentColor = Console.ForegroundColor;

            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.WriteLine("                ,/");
            Console.WriteLine("              ,'/");
            Console.WriteLine("            ,' /");
            Console.WriteLine("          ,'  /_____,");
            Console.WriteLine("        .'____    ,'        NuGet - ng.exe lightning");
            Console.WriteLine("             /  ,'");
            Console.WriteLine("            / ,'            The lightning fast catalog2registration.");
            Console.WriteLine("           /,'");
            Console.WriteLine("          /'");
            Console.ForegroundColor = currentColor;
            Console.WriteLine();
        }

        public override string GetUsage()
        {
            var sw = new StringWriter();

            sw.WriteLine($"Usage: ng lightning -{Arguments.Command} prepare|strike");
            sw.WriteLine();
            sw.WriteLine($"The following arguments are supported on both lightning commands.");
            sw.WriteLine();
            sw.WriteLine($"  -{Arguments.ContentBaseAddress} <content-address>");
            sw.WriteLine($"      The base address for package contents.");
            sw.WriteLine($"  -{Arguments.GalleryBaseAddress} <gallery-base-address>");
            sw.WriteLine($"      The base address for gallery.");
            sw.WriteLine($"  -{Arguments.ContentIsFlatContainer} true|false");
            sw.WriteLine($"      Boolean to indicate if the registration blobs will have the content from the packages or flat container.");
            sw.WriteLine($"      The default value is false.");
            sw.WriteLine($"  -{Arguments.FlatContainerName} <flat-container-name>");
            sw.WriteLine($"      It is required when the ContentIsFlatContainer flag is true.");
            sw.WriteLine($"  -{Arguments.StorageSuffix} <storage-suffix>");
            sw.WriteLine($"      String to indicate the target storage suffix. If china for example core.chinacloudapi.cn needs to be used.");
            sw.WriteLine($"      The default value is 'core.windows.net'.");
            sw.WriteLine($"  -{Arguments.AllIconsInFlatContainer} true|false");
            sw.WriteLine($"      Assume all icons (including external) are in flat container.");
            sw.WriteLine($"      The default value is false.");
            sw.WriteLine($"  -{Arguments.Driver} {GraphDriver}|{JsonDriver}");
            sw.WriteLine($"      Specifies which implementation to use when building the hives.");
            sw.WriteLine($"      '{GraphDriver}' uses RDF and JSON-LD. '{JsonDriver}' uses simple JSON serialization.");
            sw.WriteLine($"      The default value is '{GraphDriver}'.");
            sw.WriteLine($"  -{Arguments.Verbose} true|false");
            sw.WriteLine($"      Switch output verbosity on/off.");
            sw.WriteLine($"      The default value is false.");
            sw.WriteLine();
            sw.WriteLine($"  -{Arguments.StorageBaseAddress} <base-address>");
            sw.WriteLine($"      Base address to write into registration blobs.");
            sw.WriteLine($"  -{Arguments.StorageAccountName} <azure-acc>");
            sw.WriteLine($"      Azure Storage account name.");
            sw.WriteLine($"  -{Arguments.StorageKeyValue} <azure-key>");
            sw.WriteLine($"      Azure Storage account name.");
            sw.WriteLine($"  -{Arguments.StorageContainer} <container>");
            sw.WriteLine($"      Container to generate registrations in.");
            sw.WriteLine();
            sw.WriteLine($"  -{Arguments.UseCompressedStorage} true|false");
            sw.WriteLine($"      Enable or disable compressed registration blobs.");
            sw.WriteLine($"  -{Arguments.CompressedStorageBaseAddress} <base-address>");
            sw.WriteLine($"      Compressed only: Base address to write into registration blobs.");
            sw.WriteLine($"  -{Arguments.CompressedStorageAccountName} <azure-acc>");
            sw.WriteLine($"      Compressed only: Azure Storage account name.");
            sw.WriteLine($"  -{Arguments.CompressedStorageKeyValue} <azure-key>");
            sw.WriteLine($"      Compressed only: Azure Storage account name.");
            sw.WriteLine($"  -{Arguments.CompressedStorageContainer} <container>");
            sw.WriteLine($"      Compressed only: Container to generate registrations in.");
            sw.WriteLine();
            sw.WriteLine($"  -{Arguments.UseSemVer2Storage} true|false");
            sw.WriteLine($"      Enable or disable SemVer 2.0.0 registration blobs.");
            sw.WriteLine($"  -{Arguments.SemVer2StorageBaseAddress} <base-address>");
            sw.WriteLine($"      SemVer 2.0.0 only: Base address to write into registration blobs.");
            sw.WriteLine($"  -{Arguments.SemVer2StorageAccountName} <azure-acc>");
            sw.WriteLine($"      SemVer 2.0.0 only: Azure Storage account name.");
            sw.WriteLine($"  -{Arguments.SemVer2StorageKeyValue} <azure-key>");
            sw.WriteLine($"      SemVer 2.0.0 only: Azure Storage account name for SemVer 2.0.0.");
            sw.WriteLine($"  -{Arguments.SemVer2StorageContainer} <container>");
            sw.WriteLine($"      SemVer 2.0.0 only: Container to generate registrations in.");
            sw.WriteLine();
            sw.WriteLine($"The prepare command:");
            sw.WriteLine($"  ng lightning -{Arguments.Command} prepare ...");
            sw.WriteLine();
            sw.WriteLine($"  -{Arguments.OutputFolder} <output-folder>");
            sw.WriteLine($"      The folder to generate files in.");
            sw.WriteLine($"  -{Arguments.TemplateFile} <template-file>");
            sw.WriteLine($"      The lightning-template.txt that calls the strike command per batch.");
            sw.WriteLine($"      This file can be found in Ng source directory.");
            sw.WriteLine($"  -{Arguments.Source} <catalog-index-url>");
            sw.WriteLine($"      The catalog index.json URL to work with.");
            sw.WriteLine($"  -{Arguments.BatchSize} <batch-size>");
            sw.WriteLine($"      The batch size.");
            sw.WriteLine();
            sw.WriteLine($"Traverses the given catalog and, using a template file and batch size,");
            sw.WriteLine($"generates executable commands that can be run in parallel.");
            sw.WriteLine($"The generated index.txt contains an alphabetical listing of all packages");
            sw.WriteLine($"in the catalog with their entries.");
            sw.WriteLine();
            sw.WriteLine($"The strike command:");
            sw.WriteLine($"  ng lightning -{Arguments.Command} strike ...");
            sw.WriteLine();
            sw.WriteLine($"  -{Arguments.IndexFile} <index-file>");
            sw.WriteLine($"      Index file generated by the lightning prepare command.");
            sw.WriteLine($"  -{Arguments.CursorFile} <cursor-file>");
            sw.WriteLine($"      Cursor file containing range of the batch.");
            sw.WriteLine();
            sw.WriteLine($"The lightning strike command is used by the batch files generated with");
            sw.WriteLine($"the prepare command. It creates registrations for a given batch of catalog");
            sw.WriteLine($"entries.");

            return sw.ToString();
        }

        #region Shared Arguments
        private string _command;
        private bool _verbose;
        private TextWriter _log;
        private string _contentBaseAddress;
        private string _galleryBaseAddress;
        private RegistrationStorageFactories _storageFactories;
        private ShouldIncludeRegistrationPackage _shouldIncludeSemVer2ForLegacyStorageFactory;
        private RegistrationMakerCatalogItem.PostProcessGraph _postProcessGraphForLegacyStorageFactory;
        private bool _forceIconsFromFlatContainer;
        private string _driver;
        private IDictionary<string, string> _arguments;
        #endregion

        #region Prepare Arguments
        private string _outputFolder;
        private string _catalogIndex;
        private string _templateFile;
        private string _batchSize;
        #endregion

        #region Strike Arguments
        private string _indexFile;
        private string _cursorFile;
        #endregion

        protected override void Init(IDictionary<string, string> arguments, CancellationToken cancellationToken)
        {
            PrintLightning();

            // Hard code against Azure storage.
            arguments[Arguments.StorageType] = Arguments.AzureStorageType;

            // Configure the package path provider.
            var useFlatContainerAsPackageContent = arguments.GetOrDefault<bool>(Arguments.ContentIsFlatContainer, false);
            if (!useFlatContainerAsPackageContent)
            {
                RegistrationMakerCatalogItem.PackagePathProvider = new PackagesFolderPackagePathProvider();
            }
            else
            {
                var flatContainerName = arguments.GetOrThrow<string>(Arguments.FlatContainerName);
                RegistrationMakerCatalogItem.PackagePathProvider = new FlatContainerPackagePathProvider(flatContainerName);
            }

            _command = arguments.GetOrThrow<string>(Arguments.Command);
            _verbose = arguments.GetOrDefault(Arguments.Verbose, false);
            _log = _verbose ? Console.Out : new StringWriter();
            _contentBaseAddress = arguments.GetOrThrow<string>(Arguments.ContentBaseAddress);
            _galleryBaseAddress = arguments.GetOrThrow<string>(Arguments.GalleryBaseAddress);
            _storageFactories = CommandHelpers.CreateRegistrationStorageFactories(arguments, _verbose);
            _shouldIncludeSemVer2ForLegacyStorageFactory = RegistrationCollector.GetShouldIncludeRegistrationPackageForLegacyStorageFactory(_storageFactories.SemVer2StorageFactory);
            _postProcessGraphForLegacyStorageFactory = RegistrationCollector.GetPostProcessGraphForLegacyStorageFactory(_storageFactories.SemVer2StorageFactory);
            _forceIconsFromFlatContainer = arguments.GetOrDefault<bool>(Arguments.AllIconsInFlatContainer);
            _driver = arguments.GetOrDefault(Arguments.Driver, GraphDriver).ToLowerInvariant();
            // We save the arguments because the "prepare" command generates "strike" commands. Some of the arguments
            // used by "prepare" should be used when executing "strike".
            _arguments = arguments;

            if (_driver != GraphDriver && _driver != JsonDriver)
            {
                throw new NotSupportedException($"The lightning driver '{_driver}' is not supported.");
            }

            switch (_command.ToLowerInvariant())
            {
                case "charge":
                case "prepare":
                    InitPrepare(arguments);
                    break;
                case "strike":
                    InitStrike(arguments);
                    break;
                default:
                    throw new NotSupportedException($"The lightning command '{_command}' is not supported.");
            }
        }

        private void InitPrepare(IDictionary<string, string> arguments)
        {
            _outputFolder = arguments.GetOrThrow<string>(Arguments.OutputFolder);
            _catalogIndex = arguments.GetOrThrow<string>(Arguments.Source);
            _templateFile = arguments.GetOrThrow<string>(Arguments.TemplateFile);
            _batchSize = arguments.GetOrThrow<string>(Arguments.BatchSize);
        }

        private void InitStrike(IDictionary<string, string> arguments)
        {
            _indexFile = arguments.GetOrThrow<string>(Arguments.IndexFile);
            _cursorFile = arguments.GetOrThrow<string>(Arguments.CursorFile);
        }

        protected override async Task RunInternalAsync(CancellationToken cancellationToken)
        {
            switch (_command.ToLowerInvariant())
            {
                case "charge":
                case "prepare":
                    await PrepareAsync();
                    break;
                case "strike":
                    await StrikeAsync();
                    break;
                default:
                    throw new ArgumentNullException();
            }
        }

        private async Task PrepareAsync()
        {
            _log.WriteLine("Making sure folder {0} exists.", _outputFolder);
            if (!Directory.Exists(_outputFolder))
            {
                Directory.CreateDirectory(_outputFolder);
            }

            // Create reindex file
            _log.WriteLine("Start preparing lightning reindex file...");

            var latestCommit = DateTime.MinValue;
            int numberOfEntries = 0;
            string indexFile = Path.Combine(_outputFolder, "index.txt");
            string optionalArgumentsTemplate = "optionalArguments";

            using (var streamWriter = new StreamWriter(indexFile, false))
            {
                var httpMessageHandlerFactory = CommandHelpers.GetHttpMessageHandlerFactory(TelemetryService, _verbose);
                var collectorHttpClient = new CollectorHttpClient(httpMessageHandlerFactory());
                var catalogIndexReader = new CatalogIndexReader(new Uri(_catalogIndex), collectorHttpClient, TelemetryService);

                var catalogIndexEntries = await catalogIndexReader.GetEntries();

                foreach (var packageRegistrationGroup in catalogIndexEntries
                    .OrderBy(x => x.CommitTimeStamp)
                    .ThenBy(x => x.Id, StringComparer.OrdinalIgnoreCase)
                    .ThenBy(x => x.Version)
                    .GroupBy(x => x.Id, StringComparer.OrdinalIgnoreCase))
                {
                    streamWriter.WriteLine("Element@{0}. {1}", numberOfEntries++, packageRegistrationGroup.Key);

                    var latestCatalogPages = new Dictionary<string, Uri>();

                    foreach (CatalogIndexEntry catalogIndexEntry in packageRegistrationGroup)
                    {
                        string key = catalogIndexEntry.Version.ToNormalizedString();
                        if (latestCatalogPages.ContainsKey(key))
                        {
                            latestCatalogPages[key] = catalogIndexEntry.Uri;
                        }
                        else
                        {
                            latestCatalogPages.Add(key, catalogIndexEntry.Uri);
                        }

                        if (latestCommit < catalogIndexEntry.CommitTimeStamp)
                        {
                            latestCommit = catalogIndexEntry.CommitTimeStamp;
                        }
                    }

                    foreach (var latestCatalogPage in latestCatalogPages)
                    {
                        streamWriter.WriteLine("{0}", latestCatalogPage.Value);
                    }
                }
            }

            _log.WriteLine("Finished preparing lightning reindex file. Output file: {0}", indexFile);

            // Write cursor to storage
            _log.WriteLine("Start writing new cursor...");
            var storage = _storageFactories.LegacyStorageFactory.Create();
            var cursor = new DurableCursor(storage.ResolveUri("cursor.json"), storage, latestCommit)
            {
                Value = latestCommit
            };

            await cursor.SaveAsync(CancellationToken.None);
            _log.WriteLine("Finished writing new cursor.");

            // Ensure the SemVer 2.0.0 storage containers is created, if applicable. The gzipped storage account is
            // created above when we write the cursor.
            _storageFactories.SemVer2StorageFactory?.Create();

            // Write command files
            _log.WriteLine("Start preparing lightning reindex command files...");

            string templateFileContents;
            using (var templateStreamReader = new StreamReader(_templateFile))
            {
                templateFileContents = await templateStreamReader.ReadToEndAsync();
            }

            int batchNumber = 0;
            int batchSizeValue = int.Parse(_batchSize);
            for (int batchStart = 0; batchStart < numberOfEntries; batchStart += batchSizeValue)
            {
                var batchEnd = (batchStart + batchSizeValue - 1);
                if (batchEnd >= numberOfEntries)
                {
                    batchEnd = numberOfEntries - 1;
                }

                var cursorCommandFileName = "cursor" + batchNumber + ".cmd";
                var cursorTextFileName = "cursor" + batchNumber + ".txt";

                using (var cursorCommandStreamWriter = new StreamWriter(Path.Combine(_outputFolder, cursorCommandFileName)))
                using (var cursorTextStreamWriter = new StreamWriter(Path.Combine(_outputFolder, cursorTextFileName)))
                {
                    var commandStreamContents = templateFileContents;

                    var replacements = _arguments
                        .Concat(new[]
                        {
                            new KeyValuePair<string, string>("indexFile", indexFile),
                            new KeyValuePair<string, string>("cursorFile", cursorTextFileName)
                        });

                    foreach (var replacement in replacements)
                    {
                        commandStreamContents = commandStreamContents
                            .Replace($"[{replacement.Key}]", replacement.Value);
                    }

                    //the not required arguments need to be added only if they were passed in
                    //they cannot be hardcoded in the template
                    var optionalArguments = new StringBuilder();
                    AppendOptionalArgument(optionalArguments, Arguments.ContentIsFlatContainer);
                    AppendOptionalArgument(optionalArguments, Arguments.FlatContainerName);
                    AppendOptionalArgument(optionalArguments, Arguments.StorageSuffix);
                    AppendOptionalArgument(optionalArguments, Arguments.AllIconsInFlatContainer);
                    AppendOptionalArgument(optionalArguments, Arguments.Driver);
                    AppendOptionalArgument(optionalArguments, Arguments.Verbose);

                    commandStreamContents = commandStreamContents
                        .Replace($"[{optionalArgumentsTemplate}]", optionalArguments.ToString());

                    await cursorCommandStreamWriter.WriteLineAsync(commandStreamContents);
                    await cursorTextStreamWriter.WriteLineAsync(batchStart + "," + batchEnd);
                }

                batchNumber++;
            }

            _log.WriteLine("Finished preparing lightning reindex command files.");

            _log.WriteLine("You can now copy the {0} file and all cursor*.cmd, cursor*.txt", indexFile);
            _log.WriteLine("to multiple machines and run the cursor*.cmd files in parallel.");
        }

        private void AppendOptionalArgument(StringBuilder optionalArguments, string name)
        {
            if (_arguments.ContainsKey(name))
            {
                if (optionalArguments.Length > 0)
                {
                    optionalArguments.AppendLine(" ^");
                    optionalArguments.Append("    ");
                }

                optionalArguments.AppendFormat("-{0} {1}", name, _arguments[name]);
            }
        }

        private async Task StrikeAsync()
        {
            _log.WriteLine("Start lightning strike for {0}...", _cursorFile);

            // Get batch range
            int batchStart;
            int batchEnd;
            using (var cursorStreamReader = new StreamReader(_cursorFile))
            {
                var batchRange = (await cursorStreamReader.ReadLineAsync()).Split(',');
                batchStart = int.Parse(batchRange[0]);
                batchEnd = int.Parse(batchRange[1]);

                _log.WriteLine("Batch range: {0} - {1}", batchStart, batchEnd);
            }
            if (batchStart > batchEnd)
            {
                _log.WriteLine("Batch already finished.");
                return;
            }

            // Time to strike
            var httpMessageHandlerFactory = CommandHelpers.GetHttpMessageHandlerFactory(TelemetryService, _verbose);
            var collectorHttpClient = new CollectorHttpClient(httpMessageHandlerFactory());
            var serviceProvider = GetServiceProvider();
            var catalogClient = serviceProvider.GetRequiredService<ICatalogClient>();
            var registrationUpdater = serviceProvider.GetRequiredService<IRegistrationUpdater>();

            var startElement = string.Format("Element@{0}.", batchStart);
            var endElement = string.Format("Element@{0}.", batchEnd + 1);
            using (var indexStreamReader = new StreamReader(_indexFile))
            {
                string line;

                // Skip entries that are not in the current batch bounds
                do
                {
                    line = await indexStreamReader.ReadLineAsync();
                }
                while (!line.Contains(startElement));

                // Run until we're outside the current batch bounds
                while (!string.IsNullOrEmpty(line) && !line.Contains(endElement) && !indexStreamReader.EndOfStream)
                {
                    _log.WriteLine(line);

                    try
                    {
                        var packageId = line.Split(new[] { ". " }, StringSplitOptions.None).Last().Trim();

                        IStrike strike;
                        switch (_driver)
                        {
                            case JsonDriver:
                                strike = new JsonStrike(catalogClient, registrationUpdater, packageId);
                                break;
                            default:
                                strike = new GraphStrike(this, collectorHttpClient, packageId);
                                break;
                        }

                        line = await indexStreamReader.ReadLineAsync();
                        while (!string.IsNullOrEmpty(line) && !line.Contains("Element@"))
                        {
                            var url = line.TrimEnd();
                            await strike.ProcessCatalogLeafUrlAsync(url);

                            // Read next line
                            line = await indexStreamReader.ReadLineAsync();
                        }

                        await strike.FinishAsync();

                        // Update cursor file so next time we have less work to do
                        batchStart++;
                        await UpdateCursorFileAsync(_cursorFile, batchStart, batchEnd);
                    }
                    catch (Exception)
                    {
                        UpdateCursorFileAsync(_cursorFile, batchStart, batchEnd).Wait();
                        throw;
                    }
                }
            }

            await UpdateCursorFileAsync("DONE" + _cursorFile, batchStart, batchEnd);
            _log.WriteLine("Finished lightning strike for {0}.", _cursorFile);
        }

        private static Task UpdateCursorFileAsync(string cursorFileName, int startIndex, int endIndex)
        {
            using (var streamWriter = new StreamWriter(cursorFileName))
            {
                streamWriter.Write(startIndex);
                streamWriter.Write(",");
                streamWriter.Write(endIndex);
            }

            return Task.FromResult(true);
        }

        private IServiceProvider GetServiceProvider()
        {
            var services = new ServiceCollection();
            services.AddSingleton(LoggerFactory);
            services.AddLogging();
            services.Add(ServiceDescriptor.Scoped(typeof(IOptionsSnapshot<>), typeof(NonCachingOptionsSnapshot<>)));
            services.AddSingleton(new TelemetryClient());

            services.Configure<Catalog2RegistrationConfiguration>(config =>
            {
                config.LegacyBaseUrl = _arguments.GetOrDefault<string>(Arguments.StorageBaseAddress);
                config.LegacyStorageContainer = _arguments.GetOrDefault<string>(Arguments.StorageContainer);
                config.StorageConnectionString = GetConnectionString(
                    config.StorageConnectionString,
                    Arguments.StorageAccountName,
                    Arguments.StorageKeyValue,
                    Arguments.StorageSuffix);

                if (_arguments.GetOrDefault<bool>(Arguments.UseCompressedStorage))
                {
                    config.GzippedBaseUrl = _arguments.GetOrDefault<string>(Arguments.CompressedStorageBaseAddress);
                    config.GzippedStorageContainer = _arguments.GetOrDefault<string>(Arguments.CompressedStorageContainer);
                    config.StorageConnectionString = GetConnectionString(
                       config.StorageConnectionString,
                       Arguments.CompressedStorageAccountName,
                       Arguments.CompressedStorageKeyValue,
                       Arguments.StorageSuffix);
                }

                if (_arguments.GetOrDefault<bool>(Arguments.UseSemVer2Storage))
                {
                    config.SemVer2BaseUrl = _arguments.GetOrDefault<string>(Arguments.SemVer2StorageBaseAddress);
                    config.SemVer2StorageContainer = _arguments.GetOrDefault<string>(Arguments.SemVer2StorageContainer);
                    config.StorageConnectionString = GetConnectionString(
                       config.StorageConnectionString,
                       Arguments.SemVer2StorageAccountName,
                       Arguments.SemVer2StorageKeyValue,
                       Arguments.StorageSuffix);
                }

                config.GalleryBaseUrl = _arguments.GetOrThrow<string>(Arguments.GalleryBaseAddress);
                if (_arguments.GetOrThrow<bool>(Arguments.ContentIsFlatContainer))
                {
                    var contentBaseAddress = _arguments.GetOrThrow<string>(Arguments.ContentBaseAddress);
                    var flatContainerName = _arguments.GetOrThrow<string>(Arguments.FlatContainerName);
                    config.FlatContainerBaseUrl = contentBaseAddress.TrimEnd('/') + '/' + flatContainerName;
                }

                config.EnsureSingleSnapshot = true;
            });

            services.AddCatalog2Registration();

            var containerBuilder = new ContainerBuilder();
            containerBuilder.AddCatalog2Registration();
            containerBuilder.Populate(services);

            return new AutofacServiceProvider(containerBuilder.Build());
        }

        private string GetConnectionString(
            string currentConnectionString,
            string accountNameArgument,
            string accountKeyArgument,
            string endpointSuffixArgument)
        {
            var builder = new StringBuilder();
            builder.Append("DefaultEndpointsProtocol=https;");
            builder.AppendFormat("AccountName={0};", _arguments.GetOrThrow<string>(accountNameArgument));
            builder.AppendFormat("AccountKey={0};", _arguments.GetOrThrow<string>(accountKeyArgument));
            builder.AppendFormat("EndpointSuffix={0}", _arguments.GetOrDefault(endpointSuffixArgument, "core.windows.net"));

            var connectionString = builder.ToString();
            if (currentConnectionString != null && currentConnectionString != connectionString)
            {
                throw new InvalidOperationException("The same connection string must be used for all hives.");
            }

            return connectionString;
        }

        private class GraphStrike : IStrike
        {
            private readonly CollectorHttpClient _collectorHttpClient;
            private readonly string _packageId;
            private readonly LightningJob _job;
            private Dictionary<string, IGraph> _sortedGraphs;

            public GraphStrike(LightningJob job, CollectorHttpClient collectorHttpClient, string packageId)
            {
                _collectorHttpClient = collectorHttpClient;
                _packageId = packageId;
                _job = job;
                _sortedGraphs = new Dictionary<string, IGraph>();
            }

            public async Task ProcessCatalogLeafUrlAsync(string url)
            {
                // Fetch graph for package version
                var graph = await _collectorHttpClient.GetGraphAsync(new Uri(url));
                if (_sortedGraphs.ContainsKey(url))
                {
                    _sortedGraphs[url] = graph;
                }
                else
                {
                    _sortedGraphs.Add(url, graph);
                }

                // To reduce memory footprint, we're flushing out large registrations
                // in very small batches.
                if (graph.Nodes.Count() > 3000 && _sortedGraphs.Count >= 20)
                {
                    // Process graphs
                    await ProcessGraphsAsync();

                    // Destroy!
                    _sortedGraphs = new Dictionary<string, IGraph>();
                }
            }

            public async Task FinishAsync()
            {
                // Process graphs
                if (_sortedGraphs.Any())
                {
                    await ProcessGraphsAsync();
                }
            }

            private async Task ProcessGraphsAsync()
            {
                await RegistrationMaker.ProcessAsync(
                    new RegistrationKey(_packageId),
                    _sortedGraphs,
                    _job._shouldIncludeSemVer2ForLegacyStorageFactory,
                    _job._storageFactories.LegacyStorageFactory,
                    _job._postProcessGraphForLegacyStorageFactory,
                    new Uri(_job._contentBaseAddress),
                    new Uri(_job._galleryBaseAddress),
                    RegistrationCollector.PartitionSize,
                    RegistrationCollector.PackageCountThreshold,
                    _job._forceIconsFromFlatContainer,
                    _job.TelemetryService,
                    CancellationToken.None);

                if (_job._storageFactories.SemVer2StorageFactory != null)
                {
                    await RegistrationMaker.ProcessAsync(
                        new RegistrationKey(_packageId),
                        _sortedGraphs,
                        _job._storageFactories.SemVer2StorageFactory,
                        new Uri(_job._contentBaseAddress),
                        new Uri(_job._galleryBaseAddress),
                        RegistrationCollector.PartitionSize,
                        RegistrationCollector.PackageCountThreshold,
                        _job._forceIconsFromFlatContainer,
                        _job.TelemetryService,
                        CancellationToken.None);
                }
            }
        }

        private class JsonStrike : IStrike
        {
            private readonly ICatalogClient _catalogClient;
            private readonly IRegistrationUpdater _updater;
            private readonly string _packageId;
            private readonly List<string> _urls;

            public JsonStrike(
                ICatalogClient catalogClient,
                IRegistrationUpdater updater,
                string packageId)
            {
                _catalogClient = catalogClient;
                _updater = updater;
                _packageId = packageId;
                _urls = new List<string>();
            }

            public async Task ProcessCatalogLeafUrlAsync(string url)
            {
                _urls.Add(url);
                if (_urls.Count >= 127)
                {
                    await FinishAsync();
                }
            }

            public async Task FinishAsync()
            {
                if (!_urls.Any())
                {
                    return;
                }

                // Download all of the leaves.
                var urlsToDownload = new ConcurrentBag<string>(_urls);
                var leaves = new ConcurrentBag<CatalogLeaf>();
                await ParallelAsync.Repeat(
                    async () =>
                    {
                        await Task.Yield();
                        while (urlsToDownload.TryTake(out var url))
                        {
                            var leaf = await _catalogClient.GetLeafAsync(url);
                            leaves.Add(leaf);
                        }
                    });

                // Build the input to hive updaters.
                var entries = new List<CatalogCommitItem>();
                var entryToLeaf = new Dictionary<CatalogCommitItem, PackageDetailsCatalogLeaf>(
                    ReferenceEqualityComparer<CatalogCommitItem>.Default);
                foreach (var leaf in leaves)
                {
                    if (leaf.IsPackageDelete() == leaf.IsPackageDetails())
                    {
                        throw new InvalidOperationException("A catalog leaf must be either a package delete or a package details leaf.");
                    }

                    var typeUri = leaf.IsPackageDetails() ? Schema.DataTypes.PackageDetails : Schema.DataTypes.PackageDelete;

                    var catalogCommitItem = new CatalogCommitItem(
                        new Uri(leaf.Url),
                        leaf.CommitId,
                        leaf.CommitTimestamp.UtcDateTime,
                        types: null,
                        typeUris: new[] { typeUri },
                        packageIdentity: new PackageIdentity(_packageId, leaf.ParsePackageVersion()));

                    entries.Add(catalogCommitItem);

                    if (leaf.IsPackageDetails())
                    {
                        entryToLeaf.Add(catalogCommitItem, (PackageDetailsCatalogLeaf)leaf);
                    }
                }

                // Update the hives.
                await _updater.UpdateAsync(_packageId, entries, entryToLeaf);

                _urls.Clear();
            }
        }

        private interface IStrike
        {
            Task ProcessCatalogLeafUrlAsync(string url);
            Task FinishAsync();
        }
    }
}
