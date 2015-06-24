// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using Microsoft.Owin;
using Microsoft.Owin.FileSystems;
using Microsoft.Owin.StaticFiles;
using Microsoft.Owin.StaticFiles.Infrastructure;
using NuGet.Indexing;
using NuGet.Services.Metadata;
using Owin;
using System;
using System.Net;
using System.Threading;
using System.Threading.Tasks;

[assembly: OwinStartup("NuGet.Services.BasicSearch", typeof(NuGet.Services.BasicSearch.Startup))]

namespace NuGet.Services.BasicSearch
{
    public class Startup
    {
        Timer _timer;
        NuGetSearcherManager _searcherManager;
        int _gate;

        public void Configuration(IAppBuilder app)
        {
            //app.UseErrorPage();

            //  search test console

            app.Use(async (context, next) =>
            {
                if (String.Equals(context.Request.Path.Value, "/console", StringComparison.OrdinalIgnoreCase))
                {
                    // Redirect to trailing slash to maintain relative links
                    context.Response.Redirect(context.Request.PathBase + context.Request.Path + "/");
                    context.Response.StatusCode = 301;
                    return;
                }
                else if (String.Equals(context.Request.Path.Value, "/console/", StringComparison.OrdinalIgnoreCase))
                {
                    context.Request.Path = new PathString("/console/Index.html");
                }
                await next();
            });

            app.UseStaticFiles(new StaticFileOptions(new SharedOptions
            {
                RequestPath = new PathString("/console"),
                FileSystem = new EmbeddedResourceFileSystem(typeof(Startup).Assembly, "NuGet.Services.BasicSearch.Console")
            }));

            //  start the service running - the Lucene index needs to be reopened regularly on a background thread
            string searchIndexRefresh = System.Configuration.ConfigurationManager.AppSettings.Get("Search.IndexRefresh") ?? "15";
            int seconds;
            if (!int.TryParse(searchIndexRefresh, out seconds))
            {
                seconds = 120;
            }

            if (InitializeSearcherManager())
            {
                _gate = 0;
                _timer = new Timer(new TimerCallback(ReopenCallback), 0, 0, seconds * 1000);
            }

            app.Run(Invoke);
        }

        void ReopenCallback(object obj)
        {
            try
            {
                int val = Interlocked.Increment(ref _gate);
                if (val > 1)
                {
                    Interlocked.Decrement(ref _gate);
                    return;
                }

                _searcherManager.MaybeReopen();
                Interlocked.Decrement(ref _gate);
                return;
            }
            catch (Exception e)
            {
                ServiceHelpers.TraceException(e);
            }
        }

        bool InitializeSearcherManager()
        {
            try
            {
                if (SafeRoleEnvironment.IsAvailable)
                {
                    var _configurationService = new ConfigurationService();
                    string luceneDirectory = _configurationService.Get("Local.Lucene.Directory");
                    if (!string.IsNullOrEmpty(luceneDirectory))
                    {
                        string dataDirectory = _configurationService.Get("Local.Data.Directory");
                        _searcherManager = NuGetSearcherManager.CreateLocal(luceneDirectory, dataDirectory);
                    }
                    else
                    {
                        string storagePrimary = _configurationService.Get("Storage.Primary");
                        string searchIndexContainer = _configurationService.Get("Search.IndexContainer");
                        string searchDataContainer = _configurationService.Get("Search.DataContainer");

                        _searcherManager = NuGetSearcherManager.CreateAzure(storagePrimary, searchIndexContainer, searchDataContainer);
                    }

                    string registrationBaseAddress = _configurationService.Get("Search.RegistrationBaseAddress");

                    _searcherManager.RegistrationBaseAddress["http"] = MakeRegistrationBaseAddress("http", registrationBaseAddress);
                    _searcherManager.RegistrationBaseAddress["https"] = MakeRegistrationBaseAddress("https", registrationBaseAddress);
                }
                else
                {
                    string luceneDirectory = System.Configuration.ConfigurationManager.AppSettings.Get("Local.Lucene.Directory");
                    if (!string.IsNullOrEmpty(luceneDirectory))
                    {
                        string dataDirectory = System.Configuration.ConfigurationManager.AppSettings.Get("Local.Data.Directory");
                        _searcherManager = NuGetSearcherManager.CreateLocal(luceneDirectory, dataDirectory);
                    }
                    else
                    {
                        string storagePrimary = System.Configuration.ConfigurationManager.AppSettings.Get("Storage.Primary");
                        string searchIndexContainer = System.Configuration.ConfigurationManager.AppSettings.Get("Search.IndexContainer");
                        string searchDataContainer = System.Configuration.ConfigurationManager.AppSettings.Get("Search.DataContainer");

                        _searcherManager = NuGetSearcherManager.CreateAzure(storagePrimary, searchIndexContainer, searchDataContainer);
                    }

                    string registrationBaseAddress = System.Configuration.ConfigurationManager.AppSettings.Get("Search.RegistrationBaseAddress");

                    _searcherManager.RegistrationBaseAddress["http"] = MakeRegistrationBaseAddress("http", registrationBaseAddress);
                    _searcherManager.RegistrationBaseAddress["https"] = MakeRegistrationBaseAddress("https", registrationBaseAddress);
                }

                _searcherManager.Open();
                return true;
            }
            catch (Exception e)
            {
                ServiceHelpers.TraceException(e);
                return false;
            }
        }

        static Uri MakeRegistrationBaseAddress(string scheme, string registrationBaseAddress)
        {
            Uri original = new Uri(registrationBaseAddress);
            if (original.Scheme == scheme)
            {
                return original;
            }
            else
            {
                return new UriBuilder(original)
                {
                    Scheme = scheme,
                    Port = -1
                }.Uri;
            }
        }

        public async Task Invoke(IOwinContext context)
        {
            try
            {
                case "/":
                    await context.Response.WriteAsync("READY");
                    context.Response.StatusCode = (int)HttpStatusCode.OK;
                    break;
                case "/find":
                    await ServiceHelpers.WriteResponse(context, HttpStatusCode.OK, ServiceImpl.Find(context, _searcherManager));
                    break;
                case "/query":
                    await ServiceHelpers.WriteResponse(context, HttpStatusCode.OK, ServiceImpl.Query(context, _searcherManager));
                    break;
                case "/autocomplete":
                    await ServiceHelpers.WriteResponse(context, HttpStatusCode.OK, ServiceImpl.AutoComplete(context, _searcherManager));
                    break;
                case "/search/query":
                    await ServiceHelpers.WriteResponse(context, HttpStatusCode.OK, GalleryServiceImpl.Query(context, _searcherManager));
                    break;
                case "/rankings":
                    await ServiceHelpers.WriteResponse(context, HttpStatusCode.OK, ServiceInfoImpl.Rankings(context, _searcherManager));
                    break;

                //case "/stats":
                //case "/search/diag":
                //    _searcherManager.MaybeReopen();
                //    await ServiceInfoImpl.Stats(context, _searcherManager);
                //    break;
                default:
                    await context.Response.WriteAsync("unrecognized");
                    context.Response.StatusCode = (int)HttpStatusCode.NotFound;
                    break;
            }
        }
    }
}
