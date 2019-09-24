// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using NuGet.Services.Configuration;
using NuGet.Services.Metadata.Catalog;
using NuGet.Services.Metadata.Catalog.Registration;
using VDS.RDF;

namespace Ng.Jobs
{
    public class LightningJob : NgJob
    {
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

            sw.WriteLine($"Usage: ng lightning -command prepare|strike");
            sw.WriteLine();
            sw.WriteLine($"The following arguments are supported on both lightning commands.");
            sw.WriteLine();
            sw.WriteLine($"  -{Arguments.ContentBaseAddress} <content-address>");
            sw.WriteLine($"      The base address for package contents.");
            sw.WriteLine();
            sw.WriteLine($"  -{Arguments.GalleryBaseAddress} <gallery-base-address>");
            sw.WriteLine($"      The base address for gallery.");
            sw.WriteLine();
            sw.WriteLine($"  -{Arguments.Verbose} true|false");
            sw.WriteLine($"      Switch output verbosity on/off.");
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
            sw.WriteLine($"  -{Arguments.ContentIsFlatContainer} true|false");
            sw.WriteLine($"      Boolean to indicate if the registration blobs will have the content from the packages or flat container.");
            sw.WriteLine($"  -{Arguments.FlatContainerName} <the flat container name>");
            sw.WriteLine($"      It is required when the ContentIsFlatContainer flag is true.");
            sw.WriteLine($"  -{Arguments.StorageSuffix} <storageSuffix>");
            sw.WriteLine($"      String to indicate the target storage suffix. If china for example core.chinacloudapi.cn needs to be used.");
            sw.WriteLine($"      The default value is blob.core.windows.net.");
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
            sw.WriteLine($"  ng lightning -command prepare ...");
            sw.WriteLine();
            sw.WriteLine($"  -outputFolder <output-folder>");
            sw.WriteLine($"      The folder to generate files in.");
            sw.WriteLine($"  -templateFile <template-file>");
            sw.WriteLine($"      The lightning-template.txt that calls the strike command per batch.");
            sw.WriteLine($"      This file can be found in Ng source directory.");
            sw.WriteLine($"  -{Arguments.Source} <catalog-index-url>");
            sw.WriteLine($"      The catalog index.json URL to work with.");
            sw.WriteLine($"  -batchSize 2000");
            sw.WriteLine($"      The batch size.");
            sw.WriteLine();
            sw.WriteLine($"Traverses the given catalog and, using a template file and batch size,");
            sw.WriteLine($"generates executable commands that can be run in parallel.");
            sw.WriteLine($"The generated index.txt contains an alphabetical listing of all packages");
            sw.WriteLine($"in the catalog with their entries.");
            sw.WriteLine();
            sw.WriteLine($"The strike command:");
            sw.WriteLine($"  ng lightning -command strike ...");
            sw.WriteLine();
            sw.WriteLine($"  -indexFile <index-file>");
            sw.WriteLine($"      Index file generated by the lightning prepare command.");
            sw.WriteLine($"  -cursorFile <cursor-file>");
            sw.WriteLine($"      Cursor file containing range of the batch.");
            sw.WriteLine($"  [-{Arguments.AllIconsInFlatContainer} [true|false]]");
            sw.WriteLine($"      Assume all icons (including external) are in flat container.");
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
        private bool _forceIconsFromFlatContainer;
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
            // We save the arguments because the "prepare" command generates "strike" commands. Some of the arguments
            // used by "prepare" should be used when executing "strike".
            _arguments = arguments;

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
            _forceIconsFromFlatContainer = arguments.GetOrDefault<bool>(Arguments.AllIconsInFlatContainer);
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
                    .ThenBy(x => x.Id)
                    .ThenBy(x => x.Version)
                    .GroupBy(x => x.Id))
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
                {
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
                        string optionalArguments = string.Empty;
                        if (_arguments.ContainsKey(Arguments.StorageSuffix))
                        {
                            optionalArguments += $" -{Arguments.StorageSuffix} {_arguments[Arguments.StorageSuffix]}";
                        }

                        if (_arguments.ContainsKey(Arguments.ContentIsFlatContainer))
                        {
                            optionalArguments += $" -{Arguments.ContentIsFlatContainer} {_arguments[Arguments.ContentIsFlatContainer]}";
                        }

                        if (_arguments.ContainsKey(Arguments.FlatContainerName))
                        {
                            optionalArguments += $" -{Arguments.FlatContainerName} {_arguments[Arguments.FlatContainerName]}";
                        }

                        commandStreamContents = commandStreamContents
                               .Replace($"[{optionalArgumentsTemplate}]", optionalArguments);

                        await cursorCommandStreamWriter.WriteLineAsync(commandStreamContents);
                        await cursorTextStreamWriter.WriteLineAsync(batchStart + "," + batchEnd);
                    }
                }

                batchNumber++;
            }

            _log.WriteLine("Finished preparing lightning reindex command files.");

            _log.WriteLine("You can now copy the {0} file and all cursor*.cmd, cursor*.txt", indexFile);
            _log.WriteLine("to multiple machines and run the cursor*.cmd files in parallel.");
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

                        var sortedGraphs = new Dictionary<string, IGraph>();

                        line = await indexStreamReader.ReadLineAsync();
                        while (!string.IsNullOrEmpty(line) && !line.Contains("Element@"))
                        {
                            // Fetch graph for package version
                            var url = line.TrimEnd();
                            var graph = await collectorHttpClient.GetGraphAsync(new Uri(url));
                            if (sortedGraphs.ContainsKey(url))
                            {
                                sortedGraphs[url] = graph;
                            }
                            else
                            {
                                sortedGraphs.Add(url, graph);
                            }

                            // To reduce memory footprint, we're flushing out large registrations
                            // in very small batches.
                            if (graph.Nodes.Count() > 3000 && sortedGraphs.Count >= 20)
                            {
                                // Process graphs
                                await ProcessGraphsAsync(packageId, sortedGraphs);

                                // Destroy!
                                sortedGraphs = new Dictionary<string, IGraph>();
                            }

                            // Read next line
                            line = await indexStreamReader.ReadLineAsync();
                        }

                        // Process graphs
                        if (sortedGraphs.Any())
                        {
                            await ProcessGraphsAsync(packageId, sortedGraphs);
                        }

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

        private async Task ProcessGraphsAsync(string packageId, IReadOnlyDictionary<string, IGraph> sortedGraphs)
        {
            await RegistrationMaker.ProcessAsync(
                new RegistrationKey(packageId),
                sortedGraphs,
                _shouldIncludeSemVer2ForLegacyStorageFactory,
                _storageFactories.LegacyStorageFactory,
                _postProcessGraphForLegacyStorageFactory,
                new Uri(_contentBaseAddress),
                new Uri(_galleryBaseAddress),
                RegistrationCollector.PartitionSize,
                RegistrationCollector.PackageCountThreshold,
                _forceIconsFromFlatContainer,
                TelemetryService,
                CancellationToken.None);

            if (_storageFactories.SemVer2StorageFactory != null)
            {
                await RegistrationMaker.ProcessAsync(
                    new RegistrationKey(packageId),
                    sortedGraphs,
                    _storageFactories.SemVer2StorageFactory,
                    new Uri(_contentBaseAddress),
                    new Uri(_galleryBaseAddress),
                    RegistrationCollector.PartitionSize,
                    RegistrationCollector.PackageCountThreshold,
                    _forceIconsFromFlatContainer,
                    TelemetryService,
                    CancellationToken.None);
            }
        }
    }
}