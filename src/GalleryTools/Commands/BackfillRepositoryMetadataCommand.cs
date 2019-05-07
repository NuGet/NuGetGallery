// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Data.Entity;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using System.Xml;
using System.Xml.Linq;
using Autofac;
using CsvHelper;
using CsvHelper.Configuration;
using CsvHelper.TypeConversion;
using Microsoft.Extensions.CommandLineUtils;
using NuGet.Packaging;
using NuGet.Packaging.Core;
using NuGet.Services.Entities;
using NuGetGallery;
using NuGetGallery.Configuration;
using GalleryTools.Utils;

namespace GalleryTools.Commands
{
    /// <summary>
    /// This tool collect repository metadata for all packages in the DB from nuspec files in V3 flat container and updates DB with this data.
    /// Usage:
    /// 1. To collect repository metadata:
    ///     a. Configure app.config with DB information and service index URL
    ///     b. Run this tool with: GalleryTools.exe fillrepodata -c
    /// This will create a file repositoryMetadata.txt with all collected data. You can stop the job anytime and restart. cursor.txt contains current position.    
    /// 
    /// 2. To update DB:
    ///     a. Run GalleryTools.exe fillrepodata -u  
    /// This will update DB from file repositoryMetadata.txt. You can stop the job anytime and restart.
    /// </summary>
    public class BackfillRepositoryMetadataCommand
    {
        public static void Configure(CommandLineApplication config)
        {
            config.Description = "Backfill repository information for packages in the Gallery";

            CommandOption lastCreateTimeOption = config.Option("-l | --lastcreatetime", "The latest creation time of package we should check", CommandOptionType.SingleValue);
            CommandOption collectData = config.Option("-c | --collect", "Collect Repository metadata and save in file", CommandOptionType.NoValue);
            CommandOption updateDB = config.Option("-u | --update", "Update DB with Repository metadata", CommandOptionType.NoValue);
            CommandOption fileName = config.Option("-f | --file", "File to use", CommandOptionType.SingleValue);

            config.HelpOption("-? | -h | --help");
            config.OnExecute(async () =>
            {
                var builder = new ContainerBuilder();
                builder.RegisterAssemblyModules(typeof(DefaultDependenciesModule).Assembly);
                var container = builder.Build();

                var connectionString = container.Resolve<IAppConfiguration>().SqlConnectionString;
                var serviceDiscoveryUri = container.Resolve<IAppConfiguration>().ServiceDiscoveryUri;

                if (collectData.HasValue())
                {
                    DateTime lastCreateTime = DateTime.MaxValue;

                    if (lastCreateTimeOption.HasValue() && !DateTime.TryParse(lastCreateTimeOption.Value(), out lastCreateTime))
                    {
                        Console.WriteLine($"Last create time is not valid. Got: {lastCreateTimeOption.Value()}");
                        return 1;
                    }

                    CollectRepositoryMetadata.Run(connectionString, serviceDiscoveryUri, lastCreateTime).GetAwaiter().GetResult();
                }

                if (updateDB.HasValue())
                {

                    if (fileName.HasValue())
                    {
                        await UpdateDB.Run(fileName.Value(), connectionString);
                    }
                    else
                    {
                        await UpdateDB.Run(connectionString);
                    }
                }

                return 0;
            });
        }

        private static class CollectRepositoryMetadata
        {
            public const string RepositoryMetadataFileName = "repositoryMetadata.txt";
            private const string ErrorsFileName = "errors.txt";
            private const string CursorFileName = "cursor.txt";
            private const int SaveCounterAfter = 10;

            private static string _flatContainerUri;

            private static EntitiesContext _context;
            private static CsvWriter _csvWriter;
            private static Log _log;
            private static FileCursor _cursor;
            private static HttpClient _httpClient;

            public static async Task Run(string connectionString, Uri serviceDiscoveryUri, DateTime lastCreateTime)
            {
                try
                {
                    await Initialize(connectionString, serviceDiscoveryUri);

                    // Get start time from cursor.
                    var startTime = _cursor.GetCursorTime();

                    Log.LogMessage($"Start time: {startTime.ToString("u")}");

                    var packagesRepository = new EntityRepository<Package>(_context);

                    var allPackages = packagesRepository.GetAll().Where(
                        p => p.Created < lastCreateTime &&
                             p.Created > startTime &&
                             (p.PackageStatusKey == PackageStatus.Available || p.PackageStatusKey == PackageStatus.Validating));
                    allPackages = allPackages.Include(p => p.PackageRegistration).OrderBy(p => p.Key);

                    int counter = 0;

                    foreach (var package in allPackages)
                    {
                        var packageId = package.PackageRegistration.Id;
                        var version = package.NormalizedVersion;

                        try
                        {
                            var repositoryMetadata = await GetRepositoryMetadata(packageId, version);

                            if (!string.IsNullOrEmpty(repositoryMetadata.Branch) ||
                                !string.IsNullOrEmpty(repositoryMetadata.Commit) ||
                                !string.IsNullOrEmpty(repositoryMetadata.Type) ||
                                !string.IsNullOrEmpty(repositoryMetadata.Url))
                            {
                                Log.LogMessage($"Found repo information for package {packageId} {version}");
                                await WriteMetadata(new RepositoryMetadataLog(repositoryMetadata, package.Created, packageId, version));
                            }
                            else
                            {
                                Console.Write(".");
                            }
                        }
                        catch (Exception e)
                        {
                            await _log.LogError(package.PackageRegistration.Id, package.NormalizedVersion, e);
                        }

                        counter++;

                        if (counter >= SaveCounterAfter)
                        {
                            await _cursor.WriteCursor(package.Created);
                            counter = 0;
                        }
                    }
                }
                finally
                {
                    Dispose();
                }

                Console.Read();
            }

            private static async Task Initialize(string connectionString, Uri serviceDiscoveryUri)
            {
                Log.LogMessage("Initializing");

                _context = new EntitiesContext(connectionString, readOnly: true);

                var metadataFileStreamWriter = new StreamWriter(RepositoryMetadataFileName, append: true);
                metadataFileStreamWriter.AutoFlush = true;
                metadataFileStreamWriter.BaseStream.Seek(0, SeekOrigin.End);
                _csvWriter = new CsvWriter(metadataFileStreamWriter);

                _log = new Log(ErrorsFileName);
                _cursor = new FileCursor(CursorFileName);

                _httpClient = new HttpClient();
                _flatContainerUri = await GetFlatContainerUriAsync(serviceDiscoveryUri);
            }

            private static void Dispose()
            {
                Log.LogMessage("Cleaning up...");

                if (_context != null)
                {
                    _context.Dispose();
                    _context = null;
                }

                if (_csvWriter != null)
                {
                    _csvWriter.Dispose();
                    _csvWriter = null;
                }

                if (_log != null)
                {
                    _log.Dispose();
                    _log = null;
                }

                if (_cursor != null)
                {
                    _cursor.Dispose();
                    _cursor = null;
                }

                if (_httpClient != null)
                {
                    _httpClient.Dispose();
                    _httpClient = null;
                }
            }

            private static async Task<RepositoryMetadata> GetRepositoryMetadata(string id, string normalizedVersion)
            {
                string nuspecUri = $"{_flatContainerUri}/{id.ToLowerInvariant()}/{normalizedVersion.ToLowerInvariant()}/{id.ToLowerInvariant()}.nuspec";

                using (var nuspecStream = await _httpClient.GetStreamAsync(nuspecUri))
                {
                    var xml = LoadXml(nuspecStream);
                    var reader = new NuspecReader(xml);
                    return reader.GetRepositoryMetadata();
                }
            }

            private static async Task<string> GetFlatContainerUriAsync(Uri serviceDiscoveryUri)
            {
                var serviceDiscoveryClient = new ServiceDiscoveryClient(serviceDiscoveryUri);
                var result = await serviceDiscoveryClient.GetEndpointsForResourceType("PackageBaseAddress/3.0.0");
                return result.First().AbsoluteUri.TrimEnd('/');
            }

            private static async Task WriteMetadata(RepositoryMetadataLog metadata)
            {
                _csvWriter.WriteRecord(metadata);
                await _csvWriter.NextRecordAsync();
            }

            private static XDocument LoadXml(Stream stream)
            {
                var settings = new XmlReaderSettings
                {
                    IgnoreWhitespace = true,
                    IgnoreComments = true,
                    IgnoreProcessingInstructions = true,
                };
                settings.XmlResolver = null;

                using (var streamReader = new StreamReader(stream))
                using (var xmlReader = XmlReader.Create(streamReader, settings))
                {
                    return XDocument.Load(xmlReader, LoadOptions.None);
                }
            }
        }

        private static class UpdateDB
        {
            private const string CursorFileName = "db_cursor.txt";
            private const string ErrorsFileName = "errors.txt";
            private const int BatchSize = 100;

            private static EntitiesContext _context;
            private static CsvReader _csvReader;
            private static FileCursor _cursor;
            private static Log _log;

            public static async Task Run(string connectionString)
            {
                await Run(CollectRepositoryMetadata.RepositoryMetadataFileName, connectionString);
            }

            public static async Task Run(string metadataFileName, string connectionString)
            {
                if (!File.Exists(metadataFileName))
                {
                    throw new ArgumentException($"File {metadataFileName} doesn't exist");
                }

                try
                {
                    Initialize(metadataFileName, connectionString);

                    var startTime = _cursor.GetCursorTime();

                    Log.LogMessage($"Start time: {startTime.ToString("u")}");

                    var packagesRepository = new EntityRepository<Package>(_context);
                    var packages = packagesRepository.GetAll().Include(p => p.PackageRegistration);

                    int counter = 0;

                    var result = await TryReadNextMetadata();
                    RepositoryMetadataLog metadata = null;

                    while (result.success)
                    {
                        metadata = result.metadata;

                        // Skip packages with create date that we already processed in a previous run.
                        if (metadata.CreationDate >= startTime)
                        {
                            var package = packages.FirstOrDefault(p => p.PackageRegistration.Id == metadata.PackageId && p.NormalizedVersion == metadata.PackageVersion);

                            if (package != null)
                            {
                                package.RepositoryUrl = metadata.RepositoryMetadata.Url;

                                if (metadata.RepositoryMetadata.Type.Length >= 100)
                                {
                                    await _log.LogError(metadata.PackageId, metadata.PackageVersion, $"Repository type too long: {metadata.RepositoryMetadata.Type}");
                                }
                                else
                                {
                                    package.RepositoryType = metadata.RepositoryMetadata.Type;
                                }

                                counter++;
                                Console.Write(".");
                            }
                            else
                            {
                                await _log.LogError(metadata.PackageId, metadata.PackageVersion, "Couldn't find in DB");
                            }
                        }

                        if (counter >= BatchSize)
                        {
                            await CommitBatch(metadata.CreationDate);
                            counter = 0;
                        }

                        result = await TryReadNextMetadata();
                    }

                    if (counter > 0)
                    {
                        await CommitBatch(metadata.CreationDate);
                    }
                }
                finally
                {

                    Dispose();
                }

                Console.Read();
            }

            private static async Task CommitBatch(DateTime cursorTime)
            {
                await _context.SaveChangesAsync();
                await _cursor.WriteCursor(cursorTime);

                Console.Write("+");
            }

            private static void Initialize(string metadataFileName, string connectionString)
            {
                var metadataFileReader = new StreamReader(metadataFileName);

                var configuration = new CsvHelper.Configuration.Configuration() { HasHeaderRecord = false };
                configuration.RegisterClassMap<RepositoryMetadataLogMap>();
                _csvReader = new CsvReader(metadataFileReader, configuration);

                _context = new EntitiesContext(connectionString, readOnly: false);
                _cursor = new FileCursor(CursorFileName);
                _log = new Log(ErrorsFileName);
            }

            private static void Dispose()
            {
                Log.LogMessage("Cleaning up...");

                if (_context != null)
                {
                    _context.Dispose();
                    _context = null;
                }

                if (_csvReader != null)
                {
                    _csvReader.Dispose();
                    _csvReader = null;
                }

                if (_log != null)
                {
                    _log.Dispose();
                    _log = null;
                }
            }

            private static async Task<(bool success, RepositoryMetadataLog metadata)> TryReadNextMetadata()
            {
                if (!await _csvReader.ReadAsync())
                {
                    return (success: false, metadata: null);
                }

                var record = _csvReader.GetRecord<RepositoryMetadataLog>();
                return (success: true, metadata: record);
            }
        }

        private class RepositoryMetadataLog
        {
            public DateTime CreationDate { get; set; }
            public string PackageId { get; set; }
            public string PackageVersion { get; set; }
            public RepositoryMetadata RepositoryMetadata { get; set; }

            public RepositoryMetadataLog()
            {
            }

            public RepositoryMetadataLog(RepositoryMetadata repositoryMetadata, DateTime creationDate, string packageId, string packageVersion)
            {
                CreationDate = creationDate;
                PackageId = packageId;
                PackageVersion = packageVersion;
                RepositoryMetadata = repositoryMetadata;
            }
        }

        private class RepositoryMetadataLogMap : ClassMap<RepositoryMetadataLog>
        {
            public RepositoryMetadataLogMap()
            {
                Map(x => x.CreationDate).TypeConverter<DateTimeConverter>();
                Map(x => x.PackageId).Index(1);
                Map(x => x.PackageVersion).Index(2);
                Map(x => x.RepositoryMetadata.Type).Index(3);
                Map(x => x.RepositoryMetadata.Url).Index(4);
                Map(x => x.RepositoryMetadata.Branch).Index(5);
                Map(x => x.RepositoryMetadata.Commit).Index(6);
            }
        }

        private class FileCursor : IDisposable
        {
            private readonly FileStream _fileStream;
            private StreamWriter _cursorWriter;

            public FileCursor(string fileName)
            {
                if (string.IsNullOrWhiteSpace(fileName))
                {
                    throw new ArgumentException(nameof(fileName));
                }

                _fileStream = File.Open(fileName, FileMode.OpenOrCreate);
                _cursorWriter = new StreamWriter(_fileStream);
                _cursorWriter.AutoFlush = true;
            }

            public void Dispose()
            {
                if (_cursorWriter != null)
                {
                    _cursorWriter.Dispose();
                    _cursorWriter = null;
                }
            }

            public DateTime GetCursorTime()
            {
                using (var reader = new StreamReader(_fileStream, Encoding.ASCII, detectEncodingFromByteOrderMarks: false, bufferSize: 100, leaveOpen: true))
                {
                    reader.BaseStream.Seek(0, SeekOrigin.Begin);

                    var value = reader.ReadLine();

                    if (DateTime.TryParse(value, out var cursorTime))
                    {
                        return cursorTime;
                    }
                }

                return DateTime.MinValue;
            }

            public async Task WriteCursor(DateTime timestamp)
            {
                _cursorWriter.BaseStream.Seek(0, SeekOrigin.Begin);
                await _cursorWriter.WriteAsync(timestamp.ToString("o"));
            }
        }

        private class Log : IDisposable
        {
            private StreamWriter _errorsWriter;

            public Log(string errorsFileName)
            {
                if (string.IsNullOrWhiteSpace(errorsFileName))
                {
                    throw new ArgumentException(nameof(errorsFileName));
                }

                _errorsWriter = new StreamWriter(errorsFileName);
                _errorsWriter.AutoFlush = true;
                _errorsWriter.BaseStream.Seek(0, SeekOrigin.End);
            }

            public static void LogMessage(string message)
            {
                Console.WriteLine($"{DateTime.Now.ToString("u")} {message}");
            }

            public void Dispose()
            {
                if (_errorsWriter != null)
                {
                    _errorsWriter.Dispose();
                    _errorsWriter = null;
                }
            }

            public async Task LogError(string packageId, string packageVersion, Exception e)
            {
                await LogError(packageId, packageVersion, e.ToString());
            }

            public async Task LogError(string packageId, string packageVersion, string errorMessage)
            {
                string message = $"Package: {packageId}, Version: {packageVersion}, Error: {errorMessage}";

                LogMessage("Error: " + message);

                await _errorsWriter.WriteLineAsync(message);
                await _errorsWriter.WriteLineAsync();
            }
        }
    }
}
