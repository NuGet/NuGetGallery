// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using Autofac;
using CsvHelper;
using CsvHelper.Configuration;
using CsvHelper.TypeConversion;
using Knapcode.MiniZip;
using Microsoft.Extensions.CommandLineUtils;
using NuGet.Packaging;
using NuGet.Services.Entities;
using NuGetGallery;
using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.Data.SqlClient;
using System.IO;
using System.Linq;
using System.Linq.Expressions;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using System.Xml;
using System.Xml.Linq;
using GalleryTools.Utils;
using NuGet.Services.Sql;
using NuGet.Packaging.Core;
using NuGet.Versioning;
using System.Collections.Concurrent;
using System.Threading;

namespace GalleryTools.Commands
{
    public abstract class BackfillCommand<TMetadata>
    {
        private const string MetadataUpdatedMessage = "Metadata updated.";

        protected abstract string MetadataFileName { get; }

        protected virtual string ErrorsFileName => "errors.txt";

        protected virtual string CursorFileName => "cursor.txt";

        protected virtual string MonitoringCursorFileName => "monitoring_cursor.txt";

        protected virtual int CollectBatchSize => 10;

        protected virtual int UpdateBatchSize => 100;

        protected virtual int LimitTo => 0;

        protected virtual MetadataSourceType SourceType => MetadataSourceType.NuspecOnly;

        protected virtual Expression<Func<Package, object>> QueryIncludes => null;

        protected IPackageService _packageService;
        private readonly HttpClient _httpClient;

        public BackfillCommand()
        {
            _httpClient = new HttpClient();

            // We want these downloads ignored by stats pipelines - this user agent is automatically skipped.
            // See https://github.com/NuGet/NuGet.Jobs/blob/262da48ed05d0366613bbf1c54f47879aad96dcd/src/Stats.ImportAzureCdnStatistics/StatisticsParser.cs#L41
            _httpClient.DefaultRequestHeaders.TryAddWithoutValidation("User-Agent",
                "Mozilla/5.0 (compatible; MSIE 9.0; Windows NT 6.1; Trident/5.0; AppInsights)   Backfill Job: NuGet.Gallery GalleryTools");
        }

        public static void Configure<TCommand>(CommandLineApplication config) where TCommand : BackfillCommand<TMetadata>, new()
        {
            config.Description = "Backfill metadata for packages in the gallery";

            var lastCreateTimeOption = config.Option("-l | --lastcreatetime", "The latest creation time of packages we should check", CommandOptionType.SingleValue);
            var collectData = config.Option("-c | --collect", "Collect metadata and save it in a file", CommandOptionType.NoValue);
            var updateDB = config.Option("-u | --update", "Update the database with collected metadata", CommandOptionType.NoValue);
            var updateSpecific = config.Option("-i | --updatespecific", "Run the collect and update operations immediately on a specific set of packages, specified by the --file option.", CommandOptionType.NoValue);
            var fileName = config.Option("-f | --file", "The file to use", CommandOptionType.SingleValue);
            var serviceDiscoveryUri = config.Option("-s | --servicediscoveryuri", "The ServiceDiscoveryUri.", CommandOptionType.SingleValue);

            config.HelpOption("-? | -h | --help");

            config.OnExecute(async () =>
            {
                var builder = new ContainerBuilder();
                builder.RegisterAssemblyModules(typeof(DefaultDependenciesModule).Assembly);
                var container = builder.Build();

                var sqlConnectionFactory = container.Resolve<ISqlConnectionFactory>();
                var sqlConnection = await sqlConnectionFactory.CreateAsync();
                var serviceDiscoveryUriValue = new Uri(serviceDiscoveryUri.Value());

                var command = new TCommand();
                command._packageService = container.Resolve<IPackageService>();

                var metadataFileName = fileName.HasValue() ? fileName.Value() : command.MetadataFileName;

                if (updateSpecific.HasValue())
                {
                    await command.UpdateSpecific(sqlConnection, serviceDiscoveryUriValue, metadataFileName);
                }
                else
                {
                    if (collectData.HasValue())
                    {
                        var lastCreateTime = DateTime.MaxValue;

                        if (lastCreateTimeOption.HasValue())
                        {
                            var lastCreateTimeString = lastCreateTimeOption.Value();

                            if (!DateTime.TryParse(lastCreateTimeString, out lastCreateTime))
                            {
                                Console.WriteLine($"Last create time is not valid. Got: {lastCreateTimeString}");
                                return 1;
                            }
                        }

                        await command.Collect(sqlConnection, serviceDiscoveryUriValue, lastCreateTime, metadataFileName);
                    }

                    if (updateDB.HasValue())
                    {
                        await command.Update(sqlConnection, metadataFileName);
                    }
                }

                return 0;
            });
        }

        private async Task UpdateSpecific(SqlConnection connection, Uri serviceDiscoveryUri, string fileName)
        {
            var remainingPackages = ReadPackageIdentityList(fileName);
            var completedPath = fileName + ".completed";
            var completedPackages = ReadPackageIdentityList(completedPath);
            remainingPackages.ExceptWith(completedPackages);
            Console.WriteLine($"Starting update on {remainingPackages.Count} packages. {completedPackages.Count} have already been completed.");

            var flatContainerUri = await GetFlatContainerUri(serviceDiscoveryUri);

            using (var context = new EntitiesContext(connection, readOnly: false))
            using (var logger = new Logger(ErrorsFileName))
            {
                var packages = GetPackagesQuery(context);
                var toDownload = new ConcurrentBag<(PackageIdentity Identity, Package Package)>();
                var toUpdate = new ConcurrentBag<(PackageIdentity Identity, Package Package, PackageMetadata Record)>();
                var batchSize = Math.Min(CollectBatchSize, UpdateBatchSize);

                while (remainingPackages.Count > 0)
                {
                    var batch = remainingPackages.Take(batchSize).ToList();
                    Console.WriteLine($"Fetching {batch.Count} packages from DB.");
                    foreach (var identity in batch)
                    {
                        remainingPackages.Remove(identity);

                        var normalizedVersion = identity.Version.ToNormalizedString();
                        var package = await GetPackageFromDbAsync(packages, identity.Id, normalizedVersion, logger);
                        if (package == null)
                        {
                            continue;
                        }

                        logger.LogPackage(package.Id, package.NormalizedVersion, "DB record fetched.");
                        toDownload.Add((identity, package));
                    }

                    // Execute the download in parallel to improve performance.
                    Console.WriteLine($"Fetching {batch.Count} {SourceType} files from storage.");
                    await Task.WhenAll(Enumerable
                        .Range(0, 16)
                        .Select(async x =>
                        {
                            while (toDownload.TryTake(out var data))
                            {
                                var record = await ReadPackageOrNullAsync(flatContainerUri, data.Package, logger);
                                if (record == null)
                                {
                                    continue;
                                }

                                logger.LogPackage(data.Package.Id, data.Package.NormalizedVersion, "File downloaded.");
                                toUpdate.Add((data.Identity, data.Package, record));
                            }
                        })
                        .ToList());

                    while (toUpdate.TryTake(out var data))
                    {
                        UpdatePackage(data.Package, data.Record.Metadata, context);
                        completedPackages.Add(data.Identity);
                    }

                    await CommitBatch(batch.Count, completedPath, completedPackages, context, logger);
                }
            }
        }

        private static HashSet<PackageIdentity> ReadPackageIdentityList(string path)
        {
            var completedLines = File.Exists(path) ? File.ReadAllLines(path) : Array.Empty<string>();
            return completedLines
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .Select(x => x.Split(','))
                .Where(x =>
                {
                    if (x.Length == 2)
                    {
                        return true;
                    }

                    Console.WriteLine($"Skipping line without two comma-separated pieces: {string.Join(",", x)}");
                    return false;
                })
                .ToList()
                .Select(x => new PackageIdentity(x[0], NuGetVersion.Parse(x[1])))
                .Distinct()
                .OrderBy(x => x.Id, StringComparer.OrdinalIgnoreCase)
                .ThenBy(x => x.Version)
                .ToHashSet();
        }

        private static async Task CommitBatch(
            int packageCount,
            string completedPath,
            HashSet<PackageIdentity> completedPackages,
            EntitiesContext context,
            Logger logger)
        {
            logger.Log("Committing batch...");
            var count = await context.SaveChangesAsync();
            File.WriteAllLines(completedPath, completedPackages.Select(x => $"{x.Id},{x.Version}"));
            logger.Log($"{packageCount} packages saved, {count} records saved.");
        }

        public async Task Collect(SqlConnection connection, Uri serviceDiscoveryUri, DateTime? lastCreateTime, string fileName)
        {
            using (var context = new EntitiesContext(connection, readOnly: true))
            using (var cursor = new FileCursor(CursorFileName))
            using (var logger = new Logger(ErrorsFileName))
            {
                var startTime = await cursor.Read();

                logger.Log($"Starting metadata collection - Cursor time: {startTime:u}");

                IQueryable<Package> packages = GetPackagesQuery(context)
                    .Where(p => p.Created < lastCreateTime && p.Created > startTime)
                    .Where(p => p.PackageStatusKey == PackageStatus.Available)
                    .OrderBy(p => p.Created);
                if (LimitTo > 0)
                {
                    packages = packages.Take(LimitTo);
                }

                var flatContainerUri = await GetFlatContainerUri(serviceDiscoveryUri);

                using (var csv = CreateCsvWriter(fileName))
                {
                    var counter = 0;
                    var lastCreatedDate = default(DateTime?);

                    foreach (var package in packages)
                    {
                        var record = await ReadPackageOrNullAsync(flatContainerUri, package, logger);

                        if (record != null)
                        {
                            csv.WriteRecord(record);

                            await csv.NextRecordAsync();

                            logger.LogPackage(package.Id, package.NormalizedVersion, "Metadata saved");
                        }

                        counter++;

                        if (!lastCreatedDate.HasValue || lastCreatedDate < package.Created)
                        {
                            lastCreatedDate = package.Created;
                        }

                        if (counter >= CollectBatchSize)
                        {
                            logger.Log($"Writing {package.Created:u} to cursor...");
                            await cursor.Write(package.Created);
                            counter = 0;

                            // Write a monitoring cursor (not locked) so for a large job we can inspect progress
                            if (!string.IsNullOrEmpty(MonitoringCursorFileName))
                            {
                                File.WriteAllText(MonitoringCursorFileName, package.Created.ToString("G"));
                            }
                        }
                    }

                    if (counter > 0 && lastCreatedDate.HasValue)
                    {
                        await cursor.Write(lastCreatedDate.Value);
                    }
                }
            }
        }

        private async Task<PackageMetadata> ReadPackageOrNullAsync(string flatContainerUri, Package package, Logger logger)
        {
            var id = package.PackageRegistration.Id;
            var version = package.NormalizedVersion;
            var idLowered = id.ToLowerInvariant();
            var versionLowered = version.ToLowerInvariant();

            try
            {
                var metadata = default(TMetadata);

                var nuspecUri =
                    $"{flatContainerUri}/{idLowered}/{versionLowered}/{idLowered}.nuspec";
                using (var nuspecStream = await _httpClient.GetStreamAsync(nuspecUri))
                {
                    var document = LoadDocument(nuspecStream);

                    var nuspecReader = new NuspecReader(document);

                    if (SourceType == MetadataSourceType.NuspecOnly)
                    {
                        metadata = ReadMetadata(nuspecReader);
                    }
                    else if (SourceType == MetadataSourceType.Nupkg)
                    {
                        var nupkgUri =
                            $"{flatContainerUri}/{idLowered}/{versionLowered}/{idLowered}.{versionLowered}.nupkg";
                        metadata = await FetchMetadataAsync(nupkgUri, nuspecReader, id, version, logger);
                    }
                }

                if (ShouldWriteMetadata(metadata))
                {
                    return new PackageMetadata(id, version, metadata, package.Created);
                }
            }
            catch (Exception e)
            {
                await logger.LogPackageError(id, version, e);
            }

            return null;
        }

        public async Task Update(SqlConnection connection, string fileName)
        {
            if (!File.Exists(fileName))
            {
                throw new ArgumentException($"File '{fileName}' doesn't exist");
            }

            using (var context = new EntitiesContext(connection, readOnly: false))
            using (var cursor = new FileCursor(CursorFileName))
            using (var logger = new Logger(ErrorsFileName))
            {
                var startTime = await cursor.Read();

                logger.Log($"Starting database update - Cursor time: {startTime:u}");

                var packages = GetPackagesQuery(context);

                using (var csv = CreateCsvReader(fileName))
                {
                    var counter = 0;
                    var lastCreatedDate = default(DateTime?);

                    var result = await TryReadMetadata(csv);

                    while (result.Success)
                    {
                        var metadata = result.Metadata;

                        if (metadata.Created >= startTime)
                        {
                            var package = await GetPackageFromDbAsync(packages, metadata.Id, metadata.Version, logger);
                            if (package != null)
                            {
                                UpdatePackage(package, metadata.Metadata, context);
                                logger.LogPackage(metadata.Id, metadata.Version, MetadataUpdatedMessage);

                                counter++;

                                if (!lastCreatedDate.HasValue || lastCreatedDate < package.Created)
                                {
                                    lastCreatedDate = metadata.Created;
                                }
                            }
                        }

                        if (counter >= UpdateBatchSize)
                        {
                            await CommitBatch(context, cursor, logger, metadata.Created);
                            counter = 0;
                        }

                        result = await TryReadMetadata(csv);
                    }

                    if (counter > 0)
                    {
                        await CommitBatch(context, cursor, logger, lastCreatedDate);
                    }
                }
            }
        }

        private IQueryable<Package> GetPackagesQuery(EntitiesContext context)
        {
            context.SetCommandTimeout(300); // large query

            var repository = new EntityRepository<Package>(context);

            var packages = repository.GetAll().Include(p => p.PackageRegistration);
            if (QueryIncludes != null)
            {
                packages = packages.Include(QueryIncludes);
            }

            return packages.Where(p => p.PackageStatusKey == PackageStatus.Available);
        }

        private static async Task<Package> GetPackageFromDbAsync(
            IQueryable<Package> packages,
            string id,
            string normalizedVersion,
            Logger logger)
        {
            var package = packages.FirstOrDefault(p => p.PackageRegistration.Id == id && p.NormalizedVersion == normalizedVersion);
            if (package == null)
            {
                await logger.LogPackageError(id, normalizedVersion, "Could not find package in the database.");
            }

            return package;
        }

        protected virtual TMetadata ReadMetadata(NuspecReader reader) => default;

        protected virtual TMetadata ReadMetadata(IList<string> files, NuspecReader nuspecReader) => default;

        protected abstract bool ShouldWriteMetadata(TMetadata metadata);

        protected abstract void ConfigureClassMap(PackageMetadataClassMap map);

        protected abstract void UpdatePackage(Package package, TMetadata metadata, EntitiesContext context);

        private static async Task<string> GetFlatContainerUri(Uri serviceDiscoveryUri)
        {
            var client = new ServiceDiscoveryClient(serviceDiscoveryUri);

            var result = await client.GetEndpointsForResourceType("PackageBaseAddress/3.0.0");

            return result.First().AbsoluteUri.TrimEnd('/');
        }

        private async Task<TMetadata> FetchMetadataAsync(
            string nupkgUri, NuspecReader nuspecReader, string id, string version, Logger logger)
        {
            var httpZipProvider = new HttpZipProvider(_httpClient);

            var zipDirectoryReader = await httpZipProvider.GetReaderAsync(new Uri(nupkgUri));
            var zipDirectory = await zipDirectoryReader.ReadAsync();
            var files = zipDirectory
                .Entries
                .Select(x => FileNameHelper.GetZipEntryPath(x.GetName()))
                .ToList();

            return ReadMetadata(files, nuspecReader);
        }

        private static XDocument LoadDocument(Stream stream)
        {
            var settings = new XmlReaderSettings
            {
                IgnoreWhitespace = true,
                IgnoreComments = true,
                IgnoreProcessingInstructions = true,
            };
            
            // This is intentionally separate from the object initializer so that FXCop can see it.
            settings.XmlResolver = null;

            using (var streamReader = new StreamReader(stream))
            using (var xmlReader = XmlReader.Create(streamReader, settings))
            {
                return XDocument.Load(xmlReader, LoadOptions.None);
            }
        }

        private CsvWriter CreateCsvWriter(string fileName)
        {
            var configuration = CreateCsvConfiguration();

            var writer = new StreamWriter(fileName, append: true) { AutoFlush = true };

            // Seek to the end for appending...
            writer.BaseStream.Seek(0, SeekOrigin.End);

            return new CsvWriter(writer, configuration);
        }

        private CsvReader CreateCsvReader(string fileName)
        {
            var configuration = CreateCsvConfiguration();

            var reader = new StreamReader(fileName);

            var csvReader = new CsvReader(reader, configuration);
            csvReader.Configuration.MissingFieldFound = null;
            return csvReader;
        }

        private Configuration CreateCsvConfiguration()
        {
            var configuration = new Configuration
            {
                HasHeaderRecord = false,
            };

            var map = new PackageMetadataClassMap();

            ConfigureClassMap(map);

            configuration.RegisterClassMap(map);

            return configuration;
        }

        private static async Task<(bool Success, PackageMetadata Metadata)> TryReadMetadata(CsvReader reader)
        {
            if (await reader.ReadAsync())
            {
                return (true, reader.GetRecord<PackageMetadata>());
            }

            return (false, null);
        }

        private async Task CommitBatch(EntitiesContext context, FileCursor cursor, Logger logger, DateTime? cursorTime)
        {
            logger.Log("Committing batch...");

            var count = await context.SaveChangesAsync();

            if (cursorTime.HasValue)
            {
                await cursor.Write(cursorTime.Value);

                // Write a monitoring cursor (not locked) so for a large job we can inspect progress
                if (!string.IsNullOrEmpty(MonitoringCursorFileName))
                {
                    File.WriteAllText(MonitoringCursorFileName, cursorTime.Value.ToString("G"));
                }
            }

            logger.Log($"{count} packages saved.");
        }

        protected class PackageMetadata
        {
            public PackageMetadata(string id, string version, TMetadata metadata, DateTime created)
            {
                Id = id;
                Version = version;
                Metadata = metadata;
                Created = created;
            }

            public PackageMetadata()
            {
                // Used for CSV deserialization.
            }

            public string Id { get; set; }

            public string Version { get; set; }

            public TMetadata Metadata { get; set; }

            public DateTime Created { get; set; }
        }

        protected class PackageMetadataClassMap : ClassMap<PackageMetadata>
        {
            public PackageMetadataClassMap()
            {
                Map(x => x.Created).Index(0).TypeConverter<DateTimeConverter>();
                Map(x => x.Id).Index(1);
                Map(x => x.Version).Index(2);
            }
        }

        private class FileCursor : IDisposable
        {
            public FileCursor(string fileName)
            {
                Stream = File.Open(fileName, FileMode.OpenOrCreate);
                Writer = new StreamWriter(Stream) { AutoFlush = true };
            }

            public FileStream Stream { get; }

            private StreamWriter Writer { get; }

            public async Task<DateTime> Read()
            {
                using (var reader = new StreamReader(Stream, Encoding.UTF8, false, 1024, leaveOpen: true))
                {
                    reader.BaseStream.Seek(0, SeekOrigin.Begin);

                    var value = await reader.ReadLineAsync();

                    if (DateTime.TryParse(value, out var cursorTime))
                    {
                        return cursorTime;
                    }

                    return DateTime.MinValue;
                }
            }

            public Task Write(DateTime cursor)
            {
                Writer.BaseStream.Seek(0, SeekOrigin.Begin);

                return Writer.WriteAsync(cursor.ToString("o"));
            }

            public void Dispose()
            {
                Writer.Dispose();
            }
        }

        private class Logger : IDisposable
        {
            public Logger(string fileName)
            {
                var stream = File.Open(fileName, FileMode.OpenOrCreate);

                stream.Seek(0, SeekOrigin.Begin);

                Writer = new StreamWriter(stream) { AutoFlush = true };
                Lock = new SemaphoreSlim(1);
            }

            private StreamWriter Writer { get; }
            public SemaphoreSlim Lock { get; }

            public void Log(string message)
            {
                Console.WriteLine($"[{DateTime.Now:u}] {message}");
            }

            public void LogPackage(string id, string version, string message)
            {
                Log($"[{id}@{version}] {message}");
            }

            public Task LogPackageError(string id, string version, Exception exception)
            {
                return LogPackageError(id, version, exception.ToString());
            }

            public async Task LogPackageError(string id, string version, string message)
            {
                LogPackage(id, version, message);

                await Lock.WaitAsync();
                try
                {
                    await Writer.WriteLineAsync($"[{id}@{version}] {message}");
                    await Writer.WriteLineAsync();
                }
                finally
                {
                    Lock.Release();
                }
            }

            public void Dispose()
            {
                Writer.Dispose();
            }
        }

        /// <summary>
        /// This enum allows our logic to respond to a package's need for only a nupsec to determine metadata, or whether
        /// it needs access to the .nupkg for analysis of the package
        /// </summary>
        public enum MetadataSourceType
        {
            /// <summary>
            /// Just the nuspec will suffice for metadata extraction
            /// </summary>
            NuspecOnly,
            /// <summary>
            /// We need to dig deeper into the bupkg for the metadata
            /// </summary>
            Nupkg
        }
    }
}
