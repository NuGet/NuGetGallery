// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Data.Entity;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Xml;
using System.Xml.Linq;
using Autofac;
using Microsoft.Extensions.CommandLineUtils;
using NuGet.Packaging;
using NuGet.Packaging.Core;
using NuGet.Services.Search.Client;
using NuGetGallery;
using NuGetGallery.Configuration;

namespace GalleryTools.Commands
{
    /// <summary>
    /// This tool collect repository metadata for all packages in the DB from nuspec files in V3 flat container and updates DB with this data.
    /// Usage:
    /// 1. To collect repository metadata:
    ///     a. Configure app.config with DB information and service index url
    ///     b. Run this tool with: GalleryTools.exe -c
    /// This will create a file repositoryMetadata.txt with all collected data. You can stop the job anytime and restart. cursor.txt contains current position.    
    /// 
    /// 2. To update DB:
    ///     a. Run GalleryTools.exe -u  
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
            config.OnExecute(() =>
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
                        UpdateDB.Run(fileName.Value(), connectionString).GetAwaiter().GetResult();
                    }
                    else
                    {
                        UpdateDB.Run(connectionString).GetAwaiter().GetResult();
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
            private static StreamWriter _metadataFileStreamWriter;
            private static Log _log;
            private static FileCursor _cursor;
            private static HttpClient _httpClient;

            public static async Task Run(string connectionString, Uri serviceDiscoveryUri, DateTime lastCreateTime)
            {
                try
                {
                    Initialize(connectionString, serviceDiscoveryUri);

                    // Get start time from cursor.
                    var startTime = _cursor.GetCursorTime();

                    Log.LogMessage($"Start time: {startTime.ToString("u")}");

                    var packagesRepository = new EntityRepository<Package>(_context);

                    var allPackages = packagesRepository.GetAll().Where(p => p.Created < lastCreateTime && p.Created > startTime && p.PackageStatusKey == PackageStatus.Available);
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
                                await WriteMetadata(package.Created, packageId, version, repositoryMetadata);
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

            private static void Initialize(string connectionString, Uri serviceDiscoveryUri)
            {
                Log.LogMessage("Initializing");

                _context = new EntitiesContext(connectionString, readOnly: true);
                _metadataFileStreamWriter = new StreamWriter(RepositoryMetadataFileName, append: true);
                _log = new Log(ErrorsFileName);
                _cursor = new FileCursor(CursorFileName);

                _httpClient = new HttpClient();
                _flatContainerUri = GetFlatContainerUri(serviceDiscoveryUri);

                _metadataFileStreamWriter.AutoFlush = true;
                _metadataFileStreamWriter.BaseStream.Seek(0, SeekOrigin.End);
            }

            private static void Dispose()
            {
                Log.LogMessage("Cleaning up...");

                if (_context != null)
                {
                    _context.Dispose();
                    _context = null;
                }

                if (_metadataFileStreamWriter != null)
                {
                    _metadataFileStreamWriter.Dispose();
                    _metadataFileStreamWriter = null;
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
                string nuspecUri = $"{_flatContainerUri}{id.ToLowerInvariant()}/{normalizedVersion.ToLowerInvariant()}/{id.ToLowerInvariant()}.nuspec";

                using (var nuspecStream = await _httpClient.GetStreamAsync(nuspecUri))
                {
                    var xml = LoadXml(nuspecStream);
                    var reader = new NuspecReader(xml);
                    return reader.GetRepositoryMetadata();
                }
            }

            private static string GetFlatContainerUri(Uri serviceDiscoveryUri)
            {
                var serviceDiscoveryClient = new ServiceDiscoveryClient(serviceDiscoveryUri);
                return serviceDiscoveryClient.GetEndpointsForResourceType("PackageBaseAddress/3.0.0").GetAwaiter().GetResult().First().AbsoluteUri;
            }

            private static async Task WriteMetadata(DateTime creationDate, string packageId, string packageVersion, RepositoryMetadata repositoryMetadata)
            {
                await _metadataFileStreamWriter.WriteLineAsync(
                    $"{creationDate.ToString("o")},{packageId},{packageVersion},{repositoryMetadata.Type},{repositoryMetadata.Url},{repositoryMetadata.Branch},{repositoryMetadata.Commit}");
            }

            private static XDocument LoadXml(Stream stream)
            {
                var settings = new XmlReaderSettings
                {
                    IgnoreWhitespace = true,
                    IgnoreComments = true,
                    IgnoreProcessingInstructions = true,
                };

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
            private static FileCursor _cursor;
            private static Log _log;
            private static StreamReader _metadataFileReader;

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

                    var metadata = await TryReadNextMetadata();

                    while (metadata.success)
                    {
                        // Skip packages with create date that we already processed in a previous run.
                        if (metadata.createdDate >= startTime)
                        {
                            var package = packages.FirstOrDefault(p => p.PackageRegistration.Id == metadata.id && p.NormalizedVersion == metadata.version);

                            if (package != null)
                            {
                                package.RepositoryUrl = metadata.repositoryMetadata.Url;

                                if (metadata.repositoryMetadata.Type.Length >= 100)
                                {
                                    await _log.LogError(metadata.id, metadata.version, $"Respository type too long: {metadata.repositoryMetadata.Type}");
                                }
                                else
                                {
                                    package.RepositoryType = metadata.repositoryMetadata.Type;
                                }

                                counter++;
                            }
                            else
                            {
                                await _log.LogError(metadata.id, metadata.version, "Couldn't find in DB");
                            }
                        }

                        if (counter >= BatchSize)
                        {
                            await CommitBatch(metadata.createdDate);
                            counter = 0;
                        }

                        metadata = await TryReadNextMetadata();
                    }

                    if (counter > 0)
                    {
                        await CommitBatch(metadata.createdDate);
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
            }

            private static void Initialize(string metadataFileName, string connectionString)
            {
                _metadataFileReader = new StreamReader(metadataFileName);
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

                if (_metadataFileReader != null)
                {
                    _metadataFileReader.Dispose();
                    _metadataFileReader = null;
                }

                if (_log != null)
                {
                    _log.Dispose();
                    _log = null;
                }
            }

            private static async Task<(bool success, DateTime createdDate, string id, string version, RepositoryMetadata repositoryMetadata)> TryReadNextMetadata()
            {
                (bool success, DateTime createdDate, string id, string version, RepositoryMetadata repositoryMetadata) result = (false, DateTime.MinValue, string.Empty, string.Empty, null);

                var line = await _metadataFileReader.ReadLineAsync();

                if (!string.IsNullOrWhiteSpace(line))
                {
                    var splitLine = line.Split(',');

                    if (splitLine.Count() >= 5 && DateTime.TryParse(splitLine[0], out result.createdDate))
                    {
                        result.success = true;
                        result.id = splitLine[1];
                        result.version = splitLine[2];
                        result.repositoryMetadata = new RepositoryMetadata(type: splitLine[3], url: splitLine[4], branch: string.Empty, commit: string.Empty);
                    }
                }

                return result;
            }
        }

        private class FileCursor : IDisposable
        {
            private FileStream _fileStream;
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
                using (var reader = new StreamReader(_fileStream, Encoding.ASCII, detectEncodingFromByteOrderMarks: false, bufferSize:100, leaveOpen: true))
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
