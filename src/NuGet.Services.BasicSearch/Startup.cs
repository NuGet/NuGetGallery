// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Diagnostics;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using Lucene.Net.Store;
using Microsoft.Extensions.Logging;
using Microsoft.Owin;
using Microsoft.Owin.FileSystems;
using Microsoft.Owin.StaticFiles;
using Microsoft.Owin.StaticFiles.Infrastructure;
using NuGet.Indexing;
using Owin;
using SerilogWeb.Classic.Enrichers;

[assembly: OwinStartup("NuGet.Services.BasicSearch", typeof(NuGet.Services.BasicSearch.Startup))]

namespace NuGet.Services.BasicSearch
{
    public class Startup
    {
        private ILogger _logger;
        private Timer _timer;
        private NuGetSearcherManager _searcherManager;
        private int _gate;
        private ResponseWriter _responseWriter;

        public void Configuration(IAppBuilder app, IConfiguration configuration, Directory directory, ILoader loader)
        {
            // create an ILoggerFactory
            var loggerConfiguration = Logging.CreateDefaultLoggerConfiguration(withConsoleLogger: false)
                .Enrich.With<HttpRequestIdEnricher>()
                .Enrich.With<HttpRequestTraceIdEnricher>()
                .Enrich.With<HttpRequestTypeEnricher>()
                .Enrich.With<HttpRequestUrlReferrerEnricher>()
                .Enrich.With<HttpRequestUserAgentEnricher>()
                .Enrich.With<HttpRequestRawUrlEnricher>();

            var loggerFactory = Logging.CreateLoggerFactory(loggerConfiguration);

            // create a logger that is scoped to this class (only)
            _logger = loggerFactory.CreateLogger<Startup>();

            _logger.LogInformation(LogMessages.AppStartup);

            // correlate requests
            app.Use(typeof(CorrelationIdMiddleware));

            // search test console
            app.Use(async (context, next) =>
            {
                if (string.Equals(context.Request.Path.Value, "/console", StringComparison.OrdinalIgnoreCase))
                {
                    // Redirect to trailing slash to maintain relative links
                    context.Response.Redirect(context.Request.PathBase + context.Request.Path + "/");
                    context.Response.StatusCode = 301;
                    return;
                }
                else if (string.Equals(context.Request.Path.Value, "/console/", StringComparison.OrdinalIgnoreCase))
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

            // start the service running - the Lucene index needs to be reopened regularly on a background thread
            var searchIndexRefresh = configuration.Get("Search.IndexRefresh") ?? "15";
            int seconds;
            if (!int.TryParse(searchIndexRefresh, out seconds))
            {
                seconds = 120;
            }

            _logger.LogInformation(LogMessages.SearchIndexRefreshConfiguration, seconds);

            if (InitializeSearcherManager(configuration, directory, loader, loggerFactory))
            {
                var intervalInMs = seconds * 1000;

                _gate = 0;
                _timer = new Timer(ReopenCallback, 0, intervalInMs, intervalInMs);
            }

            _responseWriter = new ResponseWriter();

            app.Run(InvokeAsync);
        }

        public void Configuration(IAppBuilder app)
        {
            ServicePointManager.DefaultConnectionLimit = Int32.MaxValue;
            ServicePointManager.Expect100Continue = false;
            ServicePointManager.UseNagleAlgorithm = false;

            Configuration(app, new ConfigurationService(), null, null);
        }

        private void ReopenCallback(object state)
        {
            try
            {
                int val = Interlocked.Increment(ref _gate);
                if (val > 1)
                {
                    _logger.LogInformation(LogMessages.SearchIndexAlreadyReopened, Thread.CurrentThread.ManagedThreadId);
                    Interlocked.Decrement(ref _gate);
                    return;
                }

                _logger.LogInformation(LogMessages.SearchIndexReopenStarted, Thread.CurrentThread.ManagedThreadId);

                var stopwatch = Stopwatch.StartNew();
                try
                {
                    _searcherManager.MaybeReopen();

                    _logger.LogInformation(LogMessages.SearchIndexReopenCompleted, stopwatch.Elapsed.TotalSeconds,
                        Thread.CurrentThread.ManagedThreadId);
                }
                finally
                {
                    Interlocked.Decrement(ref _gate);
                }
            }
            catch (Exception e)
            {
                _logger.LogError(LogMessages.SearchIndexReopenFailed, e);
            }
        }

        private bool InitializeSearcherManager(IConfiguration configuration, Directory directory, ILoader loader, ILoggerFactory loggerFactory)
        {
            try
            {
                Retry.Incremental(
                    () =>
                    {
                        _searcherManager = NuGetSearcherManager.Create(configuration, loggerFactory, directory, loader);
                        _searcherManager.Open();
                    },
                    shouldRetry: e =>
                    {
                        // retry on any exception (but log it)
                        _logger.LogError("Startup: An error occurred initializing searcher manager. Going to retry...", e);
                        return true;
                    },
                    maxRetries: 5,
                    waitIncrement: TimeSpan.FromSeconds(1));
                
                return true;
            }
            catch (Exception e)
            {
                _logger.LogCritical("Startup: A critical error occurred initializing searcher manager. Number of retries exhausted.", e);
                return false;
            }
        }

        public async Task InvokeAsync(IOwinContext context)
        {
            try
            {
                if (_searcherManager == null)
                {
                    _logger.LogInformation(LogMessages.SearcherManagerNotInitialized);
                    context.Response.StatusCode = (int)HttpStatusCode.OK;
                    await context.Response.WriteAsync("UNINITIALIZED");
                }
                else
                {
                    switch (context.Request.Path.Value)
                    {
                        case "/":
                            context.Response.StatusCode = (int)HttpStatusCode.OK;
                            await context.Response.WriteAsync("READY");
                            break;
                        case "/find":
                            await ServiceEndpoints.FindAsync(context, _searcherManager, _responseWriter);
                            break;
                        case "/query":
                            await ServiceEndpoints.V3SearchAsync(context, _searcherManager, _responseWriter);
                            break;
                        case "/autocomplete":
                            await ServiceEndpoints.AutoCompleteAsync(context, _searcherManager, _responseWriter);
                            break;
                        case "/search/query":
                            await ServiceEndpoints.V2SearchAsync(context, _searcherManager, _responseWriter);
                            break;
                        case "/rankings":
                            await ServiceEndpoints.RankingsAsync(context, _searcherManager, _responseWriter);
                            break;
                        case "/search/diag":
                            await ServiceEndpoints.Stats(context, _searcherManager, _responseWriter);
                            break;
                        default:
                            context.Response.StatusCode = (int)HttpStatusCode.NotFound;
                            await context.Response.WriteAsync("UNRECOGNIZED");
                            break;
                    }
                }
            }
            catch (ClientException e)
            {
                await _responseWriter.WriteResponseAsync(context, e);
            }
            catch (Exception e)
            {
                await _responseWriter.WriteResponseAsync(context, e, _logger);
            }
        }
    }
}
