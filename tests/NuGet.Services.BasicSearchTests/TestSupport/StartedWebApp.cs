// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Configuration;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using System.Xml.Serialization;
using Microsoft.Owin.Hosting;
using NuGet.Services.BasicSearch;

namespace NuGet.Services.BasicSearchTests.TestSupport
{
    public class StartedWebApp : IDisposable
    {
        private TestSettings _settings;
        private INupkgDownloader _nupkgDownloader;
        private LuceneDirectoryInitializer _luceneDirectoryInitializer;
        private DataDirectoryInitializer _dataDirectoryInitializer;
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
            _dataDirectoryInitializer = new DataDirectoryInitializer(_settings);
            _portReserver = new PortReserver();

            // set up the data
            var enumeratedPackages = packages?.ToArray() ?? new PackageVersion[0];
            await _nupkgDownloader.DownloadPackagesAsync(enumeratedPackages);
            string luceneDirectory = _luceneDirectoryInitializer.GetInitializedDirectory(enumeratedPackages);
            string dataDirectory = await _dataDirectoryInitializer.GetInitializedDirectoryAsync();

            // set up the configuration
            ConfigurationManager.AppSettings.Set("Local.Lucene.Directory", luceneDirectory);
            ConfigurationManager.AppSettings.Set("Local.Data.Directory", dataDirectory);
            ConfigurationManager.AppSettings.Set("Search.RegistrationBaseAddress", _settings.RegistrationBaseAddress);

            // start the app
            _webApp = WebApp.Start<Startup>(_portReserver.BaseUri);
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