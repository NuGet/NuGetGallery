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
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using System.Xml;
using System.Xml.Linq;
using GalleryTools.Utils;
using NuGet.Services.Sql;

namespace GalleryTools.Commands
{
    public abstract class BackfillCommand<TMetadata>
    {
        protected abstract string MetadataFileName { get; }

        protected virtual string ErrorsFileName => "errors.txt";

        protected virtual string CursorFileName => "cursor.txt";

        protected virtual int CollectBatchSize => 10;

        protected virtual int UpdateBatchSize => 100;

        protected virtual int LimitTo => 0;

        protected virtual MetadataSourceType SourceType => MetadataSourceType.Nuspec;

        protected virtual bool UpdateNeedsContext => false;

        protected virtual string QueryIncludes => null;

        protected IPackageService _packageService;

        public static void Configure<TCommand>(CommandLineApplication config) where TCommand : BackfillCommand<TMetadata>, new()
        {
            config.Description = "Backfill metadata for packages in the gallery";

            var lastCreateTimeOption = config.Option("-l | --lastcreatetime", "The latest creation time of packages we should check", CommandOptionType.SingleValue);
            var collectData = config.Option("-c | --collect", "Collect metadata and save it in a file", CommandOptionType.NoValue);
            var updateDB = config.Option("-u | --update", "Update the database with collected metadata", CommandOptionType.NoValue);
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

                return 0;
            });
        }

        public async Task Collect(SqlConnection connection, Uri serviceDiscoveryUri, DateTime? lastCreateTime, string fileName)
        {
            using (var context = new EntitiesContext(connection, readOnly: true))
            using (var cursor = new FileCursor(CursorFileName))
            using (var logger = new Logger(ErrorsFileName))
            {
                context.SetCommandTimeout(300); // large query

                var startTime = await cursor.Read();

                logger.Log($"Starting metadata collection - Cursor time: {startTime:u}");

                var repository = new EntityRepository<Package>(context);

                var packages = repository.GetAll()
                    .Include(p => p.PackageRegistration);
                if (QueryIncludes != null)
                {
                    packages = packages.Include(QueryIncludes);
                }
                
                packages = packages
                    .Where(p => p.Created < lastCreateTime && p.Created > startTime)
                    .OrderBy(p => p.PackageRegistration.Id);
                if (LimitTo > 0)
                {
                    packages = packages.Take(LimitTo);
                }

                var flatContainerUri = await GetFlatContainerUri(serviceDiscoveryUri);

                using (var csv = CreateCsvWriter(fileName))
                using (var http = new HttpClient())
                {
                    var counter = 0;
                    var lastCreatedDate = default(DateTime?);

                    foreach (var package in packages)
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
                            using (var nuspecStream = await http.GetStreamAsync(nuspecUri))
                            {
                                var document = LoadDocument(nuspecStream);

                                var nuspecReader = new NuspecReader(document);

                                if (SourceType == MetadataSourceType.Nuspec)
                                {
                                    metadata = ReadMetadata(nuspecReader);
                                }
                                else if (SourceType == MetadataSourceType.Entities)
                                {
                                    var nupkgUri =
                                        $"{flatContainerUri}/{idLowered}/{versionLowered}/{idLowered}.{versionLowered}.nupkg";
                                    metadata = await FetchMetadataAsync(http, nupkgUri, nuspecReader);
                                }
                            }

                            if (ShouldWriteMetadata(metadata))
                            {
                                var record = new PackageMetadata(id, version, metadata, package.Created);

                                csv.WriteRecord(record);

                                await csv.NextRecordAsync();

                                logger.LogPackage(id, version, "Metadata saved.");
                            }
                        }
                        catch (Exception e)
                        {
                            await logger.LogPackageError(id, version, e);
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
                        }
                    }

                    if (counter > 0 && lastCreatedDate.HasValue)
                    {
                        await cursor.Write(lastCreatedDate.Value);
                    }
                }
            }
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

                var repository = new EntityRepository<Package>(context);

                var packages = repository.GetAll().Include(p => p.PackageRegistration);

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
                            var package = packages.FirstOrDefault(p => p.PackageRegistration.Id == metadata.Id && p.NormalizedVersion == metadata.Version);

                            if (package != null)
                            {
                                if (UpdateNeedsContext)
                                {
                                    UpdatePackage(context, package, metadata.Metadata);
                                }
                                else
                                {
                                    UpdatePackage(package, metadata.Metadata);
                                }

                                logger.LogPackage(metadata.Id, metadata.Version, "Metadata updated.");

                                counter++;

                                if (!lastCreatedDate.HasValue || lastCreatedDate < package.Created)
                                {
                                    lastCreatedDate = metadata.Created;
                                }
                            }
                            else
                            {
                                await logger.LogPackageError(metadata.Id, metadata.Version, "Could not find package in the database.");
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

        protected virtual TMetadata ReadMetadata(NuspecReader reader) => default;

        protected virtual TMetadata ReadMetadata(IList<string> files, NuspecReader nuspecReader) => default;

        protected abstract bool ShouldWriteMetadata(TMetadata metadata);

        protected abstract void ConfigureClassMap(PackageMetadataClassMap map);

        protected virtual void UpdatePackage(Package package, TMetadata metadata)
        {
            throw new NotImplementedException();
        }

        protected virtual void UpdatePackage(EntitiesContext context, Package package, TMetadata metadata)
        {
            throw new NotImplementedException();
        }

        private static async Task<string> GetFlatContainerUri(Uri serviceDiscoveryUri)
        {
            var client = new ServiceDiscoveryClient(serviceDiscoveryUri);

            var result = await client.GetEndpointsForResourceType("PackageBaseAddress/3.0.0");

            return result.First().AbsoluteUri.TrimEnd('/');
        }

        private async Task<TMetadata> FetchMetadataAsync(HttpClient httpClient, string nupkgUri, NuspecReader nuspecReader)
        {
            var httpZipProvider = new HttpZipProvider(httpClient);
            httpZipProvider.RequireAcceptRanges = false;
            httpZipProvider.RequireContentRange = false;

            try
            {
                var zipDirectoryReader = await httpZipProvider.GetReaderAsync(new Uri(nupkgUri));
                var zipDirectory = await zipDirectoryReader.ReadAsync();
                var files = zipDirectory
                    .Entries
                    .Select(x => x.GetName())
                    .ToList();

                return ReadMetadata(files, nuspecReader);
            }
            catch (Exception)
            {
                return default; // fail silently without a value
            }
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

            return new CsvReader(reader, configuration);
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
            }

            private StreamWriter Writer { get; }

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

                await Writer.WriteLineAsync($"[{id}@{version}] {message}");
                await Writer.WriteLineAsync();
            }

            public void Dispose()
            {
                Writer.Dispose();
            }
        }

        public enum MetadataSourceType
        {
            Nuspec,
            Entities
        }
    }
}
