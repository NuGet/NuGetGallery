// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using System.Xml.Serialization;
using Lucene.Net.Store;
using Microsoft.Owin.Hosting;
using NuGet.Services.BasicSearch;

namespace NuGet.Services.BasicSearchTests.TestSupport
{
    public class StartedWebApp : IDisposable
    {
        private TestSettings _settings;
        private INupkgDownloader _nupkgDownloader;
        private LuceneDirectoryInitializer _luceneDirectoryInitializer;
        private PortReserver _portReserver;
        private IDisposable _webApp;

        private StartedWebApp()
        {
        }

        public static async Task<StartedWebApp> StartAsync(IEnumerable<PackageVersion> packages = null)
        {
            var startedWebApp = new StartedWebApp();
            await startedWebApp.InitializeAsync(packages);
            return startedWebApp;
        }

        private async Task InitializeAsync(IEnumerable<PackageVersion> packages = null)
        {
            // establish the settings
            _settings = ReadFromXml<TestSettings>("TestSettings.xml");
            _nupkgDownloader = new NupkgDownloader(_settings);
            _luceneDirectoryInitializer = new LuceneDirectoryInitializer(_settings, _nupkgDownloader);
            _portReserver = new PortReserver();

            // set up the data
            var enumeratedPackages = packages?.ToArray() ?? new PackageVersion[0];
            await _nupkgDownloader.DownloadPackagesAsync(enumeratedPackages);
            var luceneDirectory = _luceneDirectoryInitializer.GetInitializedDirectory(enumeratedPackages);

            // set up the configuration
            var configuration = new InMemoryConfiguration
            {
                { "Local.Lucene.Directory", (luceneDirectory as FSDirectory)?.Directory.FullName ?? "RAM" },
                { "Search.RegistrationBaseAddress", _settings.RegistrationBaseAddress }
            };

            // set up the data directory
            var loader = new InMemoryLoader
            {
                { "downloads.v1.json", "[]" },
                { "curatedfeeds.json", "[]" },
                { "owners.json", "[]" },
                { "rankings.v1.json", "{\"Rank\": []}" }
            };

            // start the app
            _webApp = WebApp.Start(_portReserver.BaseUri, app => new Startup().Configuration(app, configuration, luceneDirectory, loader));
            Client = new HttpClient { BaseAddress = new Uri(_portReserver.BaseUri) };
        }

        public HttpClient Client { get; private set; }

        public void Dispose()
        {
            Client?.Dispose();
            _webApp?.Dispose();
            _portReserver?.Dispose();
        }

        private static T ReadFromXml<T>(string path)
        {
            var xmlSerializer = new XmlSerializer(typeof(T));
            using (var stream = new FileStream(path, FileMode.Open, FileAccess.Read))
            {
                return (T)xmlSerializer.Deserialize(stream);
            }
        }
    }
}