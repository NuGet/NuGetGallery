// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.
using System;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Owin;
using Microsoft.Owin.FileSystems;
using Microsoft.Owin.StaticFiles;
using Microsoft.Owin.StaticFiles.Infrastructure;
using NuGet.Indexing;
using Owin;
using Lucene.Net.QueryParsers;

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

            _searcherManager = CreateSearcherManager();

            _searcherManager.Open();

            _gate = 0;
            _timer = new Timer(new TimerCallback(ReopenCallback), 0, 0, seconds * 1000);

            app.Run(Invoke);
        }

        void ReopenCallback(object obj)
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

        public NuGetSearcherManager CreateSearcherManager()
        {
            NuGetSearcherManager searcherManager;

            string luceneDirectory = System.Configuration.ConfigurationManager.AppSettings.Get("Local.Lucene.Directory");
            if (!string.IsNullOrEmpty(luceneDirectory))
            {
                string dataDirectory = System.Configuration.ConfigurationManager.AppSettings.Get("Local.Data.Directory");
                searcherManager = NuGetSearcherManager.CreateLocal(luceneDirectory, dataDirectory);
            }
            else
            {
                string storagePrimary = System.Configuration.ConfigurationManager.AppSettings.Get("Storage.Primary");
                string searchIndexContainer = System.Configuration.ConfigurationManager.AppSettings.Get("Search.IndexContainer");
                string searchDataContainer = System.Configuration.ConfigurationManager.AppSettings.Get("Search.DataContainer");

                searcherManager = NuGetSearcherManager.CreateAzure(storagePrimary, searchIndexContainer, searchDataContainer);
            }

            string registrationBaseAddress = System.Configuration.ConfigurationManager.AppSettings.Get("Search.RegistrationBaseAddress");

            searcherManager.RegistrationBaseAddress["http"] = MakeRegistrationBaseAddress("http", registrationBaseAddress);
            searcherManager.RegistrationBaseAddress["https"] = MakeRegistrationBaseAddress("https", registrationBaseAddress);
            return searcherManager;
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
                switch (context.Request.Path.Value)
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
            catch (ClientException e)
            {
                ServiceHelpers.WriteResponse(context, e);
            }
            catch (Exception e)
            {
                ServiceHelpers.WriteResponse(context, e);
            }
        }
    }
}
