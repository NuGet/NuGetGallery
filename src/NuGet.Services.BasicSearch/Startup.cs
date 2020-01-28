// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using System.Web.Cors;
using Lucene.Net.Store;
using Microsoft.ApplicationInsights.DependencyCollector;
using Microsoft.ApplicationInsights.Extensibility;
using Microsoft.ApplicationInsights.Extensibility.Implementation;
using Microsoft.ApplicationInsights.Extensibility.PerfCounterCollector;
using Microsoft.ApplicationInsights.Extensibility.PerfCounterCollector.QuickPulse;
using Microsoft.ApplicationInsights.Web;
using Microsoft.ApplicationInsights.WindowsServer;
using Microsoft.Azure.KeyVault;
using Microsoft.Extensions.Logging;
using Microsoft.IdentityModel.Clients.ActiveDirectory;
using Microsoft.Owin;
using Microsoft.Owin.Cors;
using Microsoft.Owin.FileSystems;
using Microsoft.Owin.StaticFiles;
using Microsoft.Owin.StaticFiles.Infrastructure;
using NuGet.ApplicationInsights.Owin;
using NuGet.Indexing;
using NuGet.Services.BasicSearch.Configuration;
using NuGet.Services.Configuration;
using NuGet.Services.Logging;
using Owin;
using SerilogWeb.Classic.Enrichers;

[assembly: OwinStartup("NuGet.Services.BasicSearch", typeof(NuGet.Services.BasicSearch.Startup))]

namespace NuGet.Services.BasicSearch
{
    public class Startup
    {
        private ILogger _logger;
        private Timer _indexReloadTimer;
        private NuGetSearcherManager _searcherManager;
        private int _gate;
        private ResponseWriter _responseWriter;
        private SearchTelemetryClient _searchTelemetryClient;
        private IConfigurationFactory _configFactory;

        public void Configuration(
            IAppBuilder app,
            IConfigurationFactory configFactory,
            Directory directory,
            ILoader loader)
        {
            _configFactory = configFactory;
            var config = GetConfiguration().Result;

            // Configure
            var applicationInsightsConfiguration = InitializeApplicationInsights(config);

            // Create an ILoggerFactory
            var loggerConfiguration = LoggingSetup.CreateDefaultLoggerConfiguration(withConsoleLogger: false)
                .Enrich.With<HttpRequestIdEnricher>()
                .Enrich.With<HttpRequestTraceIdEnricher>()
                .Enrich.With<HttpRequestTypeEnricher>()
                .Enrich.With<HttpRequestUrlReferrerEnricher>()
                .Enrich.With<HttpRequestUserAgentEnricher>()
                .Enrich.With<HttpRequestRawUrlEnricher>();

            var loggerFactory = LoggingSetup.CreateLoggerFactory(
                loggerConfiguration,
                telemetryConfiguration: applicationInsightsConfiguration.TelemetryConfiguration);

            // Create a logger that is scoped to this class (only)
            _logger = loggerFactory.CreateLogger<Startup>();

            _logger.LogInformation(LogMessages.AppStartup);

            // Overwrite the index's Azure Directory cache path if configured ot use an Azure Local Storage resource.
            if (!string.IsNullOrEmpty(config.AzureDirectoryCacheLocalResourceName))
            {
                if (SafeRoleEnvironment.TryGetLocalResourceRootPath(config.AzureDirectoryCacheLocalResourceName, out var path))
                {
                    config.AzureDirectoryCachePath = path;

                    _logger.LogInformation(
                        "Set Azure Directory cache path to Azure Local Resource = {LocalResourceName}, Path = {LocalResourcePath}",
                        config.AzureDirectoryCacheLocalResourceName,
                        config.AzureDirectoryCachePath);
                }
                else
                {
                    _logger.LogWarning(
                        "Cannot use Azure Local Resource {LocalResourceName} for caching when the RoleEnvironment is not available",
                        config.AzureDirectoryCacheLocalResourceName);
                }
            }

            // redirect all HTTP requests to HTTPS
            if (config.RequireSsl)
            {
                if (string.IsNullOrWhiteSpace(config.ForceSslExclusion))
                {
                    app.UseForceSsl(config.SslPort);
                }
                else
                {
                    app.UseForceSsl(config.SslPort, new[] { config.ForceSslExclusion });
                }
            }

            // Correlate requests
            app.Use(typeof(CorrelationIdMiddleware));

            // Add Application Insights
            app.Use(typeof(RequestTrackingMiddleware), new[] { applicationInsightsConfiguration.TelemetryConfiguration });

            // Set up exception logging
            app.Use(typeof(ExceptionTrackingMiddleware), new[] { applicationInsightsConfiguration.TelemetryConfiguration });

            // Enable HSTS
            app.Use(async (context, next) =>
            {
                context.Response.Headers.Add("Strict-Transport-Security", new string[] { "max-age=31536000; includeSubDomains" });
                await next.Invoke();
            });

            // Disable content type sniffing
            app.Use(async (context, next) =>
            {
                context.Response.Headers.Add("X-Content-Type-Options", new[] { "nosniff" });
                await next.Invoke();
            });

            // Enable CORS
            var corsPolicy = new CorsPolicy
            {
                Methods = { "GET", "HEAD", "OPTIONS" },
                Headers = { "Content-Type", "If-Match", "If-Modified-Since", "If-None-Match", "If-Unmodified-Since", "Accept-Encoding" },
                ExposedHeaders = { "Content-Type", "Content-Length", "Last-Modified", "Transfer-Encoding", "ETag", "Date", "Vary", "Server", "X-Hit", "X-CorrelationId" },
                AllowAnyOrigin = true,
                PreflightMaxAge = 3600
            };

            app.UseCors(new CorsOptions
            {
                PolicyProvider = new CorsPolicyProvider
                {
                    PolicyResolver = context => Task.FromResult(corsPolicy)
                }
            });

            // Search test console
            app.Use(typeof(SearchConsoleMiddleware));
            app.UseStaticFiles(new StaticFileOptions(new SharedOptions
            {
                RequestPath = new PathString("/console"),
                FileSystem = new EmbeddedResourceFileSystem(typeof(Startup).Assembly, "NuGet.Services.BasicSearch.Console")
            }));

            // Start the service running - the Lucene index needs to be reopened regularly on a background thread
            var intervalSec = config.IndexRefreshSec;

            _logger.LogInformation(LogMessages.SearchIndexRefreshConfiguration, intervalSec);

            if (InitializeSearcherManager(config, directory, loader, loggerFactory))
            {
                var intervalMs = intervalSec * 1000;

                _gate = 0;
                _indexReloadTimer = new Timer(ReopenCallback, 0, intervalMs, intervalMs);
            }

            _responseWriter = new ResponseWriter();

            app.Run(InvokeAsync);
        }

        private ApplicationInsightsConfiguration InitializeApplicationInsights(BasicSearchConfiguration config)
        {
            var applicationInsightsConfiguration = Logging.ApplicationInsights.Initialize(
                                config.ApplicationInsightsInstrumentationKey,
                                TimeSpan.FromSeconds(config.ApplicationInsightsHeartbeatIntervalSeconds));

            RegisterApplicationInsightsTelemetryModules(applicationInsightsConfiguration.TelemetryConfiguration);

            // Add telemetry initializers
            applicationInsightsConfiguration.TelemetryConfiguration.TelemetryInitializers.Add(new MachineNameTelemetryInitializer());
            applicationInsightsConfiguration.TelemetryConfiguration.TelemetryInitializers.Add(new DeploymentIdTelemetryInitializer());

            // Create telemetry sink
            _searchTelemetryClient = new SearchTelemetryClient(applicationInsightsConfiguration.TelemetryConfiguration);

            // Add telemetry processors
            var processorChain = applicationInsightsConfiguration.TelemetryConfiguration.TelemetryProcessorChainBuilder;

            processorChain.Use(next =>
            {
                var processor = new RequestTelemetryProcessor(next);

                processor.SuccessfulResponseCodes.Add(400);
                processor.SuccessfulResponseCodes.Add(404);

                return processor;
            });

            processorChain.Use(next => new ExceptionTelemetryProcessor(next, _searchTelemetryClient.TelemetryClient));

            processorChain.Build();

            return applicationInsightsConfiguration;
        }

        private static void RegisterApplicationInsightsTelemetryModules(TelemetryConfiguration configuration)
        {
            RegisterApplicationInsightsTelemetryModule(
                new AppServicesHeartbeatTelemetryModule(),
                configuration);

            RegisterApplicationInsightsTelemetryModule(
                new AzureInstanceMetadataTelemetryModule(),
                configuration);

            RegisterApplicationInsightsTelemetryModule(
                new DeveloperModeWithDebuggerAttachedTelemetryModule(),
                configuration);

            RegisterApplicationInsightsTelemetryModule(
                new UnhandledExceptionTelemetryModule(),
                configuration);

            RegisterApplicationInsightsTelemetryModule(
                new UnobservedExceptionTelemetryModule(),
                configuration);

            var requestTrackingModule = new RequestTrackingTelemetryModule();
            requestTrackingModule.Handlers.Add("Microsoft.VisualStudio.Web.PageInspector.Runtime.Tracing.RequestDataHttpHandler");
            requestTrackingModule.Handlers.Add("System.Web.StaticFileHandler");
            requestTrackingModule.Handlers.Add("System.Web.Handlers.AssemblyResourceLoader");
            requestTrackingModule.Handlers.Add("System.Web.Optimization.BundleHandler");
            requestTrackingModule.Handlers.Add("System.Web.Script.Services.ScriptHandlerFactory");
            requestTrackingModule.Handlers.Add("System.Web.Handlers.TraceHandler");
            requestTrackingModule.Handlers.Add("System.Web.Services.Discovery.DiscoveryRequestHandler");
            requestTrackingModule.Handlers.Add("System.Web.HttpDebugHandler");
            RegisterApplicationInsightsTelemetryModule(
                requestTrackingModule,
                configuration);

            RegisterApplicationInsightsTelemetryModule(
                new ExceptionTrackingTelemetryModule(),
                configuration);

            RegisterApplicationInsightsTelemetryModule(
                new AspNetDiagnosticTelemetryModule(),
                configuration);

            var dependencyTrackingModule = new DependencyTrackingTelemetryModule();
            dependencyTrackingModule.ExcludeComponentCorrelationHttpHeadersOnDomains.Add("core.windows.net");
            dependencyTrackingModule.ExcludeComponentCorrelationHttpHeadersOnDomains.Add("core.chinacloudapi.cn");
            dependencyTrackingModule.ExcludeComponentCorrelationHttpHeadersOnDomains.Add("core.cloudapi.de");
            dependencyTrackingModule.ExcludeComponentCorrelationHttpHeadersOnDomains.Add("core.usgovcloudapi.net");
            dependencyTrackingModule.IncludeDiagnosticSourceActivities.Add("Microsoft.Azure.EventHubs");
            dependencyTrackingModule.IncludeDiagnosticSourceActivities.Add("Microsoft.Azure.ServiceBus");
            RegisterApplicationInsightsTelemetryModule(
                dependencyTrackingModule,
                configuration);

            RegisterApplicationInsightsTelemetryModule(
                new PerformanceCollectorModule(),
                configuration);

            RegisterApplicationInsightsTelemetryModule(
                new QuickPulseTelemetryModule(),
                configuration);
        }

        private static void RegisterApplicationInsightsTelemetryModule(ITelemetryModule telemetryModule, TelemetryConfiguration configuration)
        {
            var existingModule = TelemetryModules.Instance.Modules.SingleOrDefault(m => m.GetType().Equals(telemetryModule.GetType()));
            if (existingModule != null)
            {
                TelemetryModules.Instance.Modules.Remove(existingModule);
            }

            telemetryModule.Initialize(configuration);

            TelemetryModules.Instance.Modules.Add(telemetryModule);
        }


        public void Configuration(IAppBuilder app)
        {
            ServicePointManager.DefaultConnectionLimit = Int32.MaxValue;
            ServicePointManager.Expect100Continue = false;
            ServicePointManager.UseNagleAlgorithm = false;

            // Ensure that SSLv3 is disabled and that Tls v1.2 is enabled.
            ServicePointManager.SecurityProtocol &= ~SecurityProtocolType.Ssl3;
            ServicePointManager.SecurityProtocol |= SecurityProtocolType.Tls12;

            var secretReaderFactory = new SecretReaderFactory();
            var secretReader = secretReaderFactory.CreateSecretReader();

            var configurationProvider =
                new EnvironmentSettingsConfigurationProvider(secretReaderFactory.CreateSecretInjector(secretReader));

            Configuration(app, new ConfigurationFactory(configurationProvider), null, null);
        }

        private async Task<BasicSearchConfiguration> GetConfiguration()
        {
            try
            {
                return await _configFactory.Get<BasicSearchConfiguration>();
            }
            catch (KeyVaultClientException e)
            {
                // The status code we expect is (e.Status == HttpStatusCode.Unauthorized || e.Status == HttpStatusCode.Forbidden) but the catch is not explicit since confidence here is low.

                // A hack related to: https://github.com/nuget/engineering/issues/2412 This can be removed/ignored after AAD tenant migration.
                TokenCache.DefaultShared.Clear();
                HttpBearerChallengeCache.GetInstance().Clear();

                _logger.LogWarning("Failed to get config from KeyVault. Cleared token cache. Error details: {Error}", e.ToString());
            }

            return await _configFactory.Get<BasicSearchConfiguration>();
        }

        private async void ReopenCallback(object state)
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

                try
                {
                    var stopwatch = Stopwatch.StartNew();

                    var newConfig = await GetConfiguration();
                    _searcherManager.MaybeReopen(newConfig);

                    stopwatch.Stop();

                    _logger.LogInformation(LogMessages.SearchIndexReopenCompleted, stopwatch.Elapsed.TotalSeconds,
                        Thread.CurrentThread.ManagedThreadId);

                    _searchTelemetryClient.TrackMetric(
                        SearchTelemetryClient.MetricName.SearchIndexReopenDuration, stopwatch.Elapsed.TotalSeconds);

                    TrackIndexMetrics(_searcherManager, _searchTelemetryClient);
                }
                finally
                {
                    Interlocked.Decrement(ref _gate);
                }
            }
            catch (Exception e)
            {
                _logger.LogError(LogMessages.SearchIndexReopenFailed, e);

                _searchTelemetryClient.TrackMetric(SearchTelemetryClient.MetricName.SearchIndexReopenFailed, 1);
            }
        }

        private bool InitializeSearcherManager(IndexingConfiguration config, Directory directory, ILoader loader, ILoggerFactory loggerFactory)
        {
            const int maxRetries = 10;

            try
            {
                Retry.Incremental(
                    () =>
                    {
                        var stopwatch = Stopwatch.StartNew();

                        _searcherManager = NuGetSearcherManager.Create(config, loggerFactory, directory, loader);
                        _searcherManager.Open();

                        stopwatch.Stop();

                        _searchTelemetryClient.TrackMetric(
                            SearchTelemetryClient.MetricName.SearchIndexReopenDuration, stopwatch.Elapsed.TotalSeconds);

                        TrackIndexMetrics(_searcherManager, _searchTelemetryClient);
                    },
                    shouldRetry: e =>
                    {
                        // Retry on any exception (but log it)
                        _logger.LogError("Startup: An error occurred initializing searcher manager. Going to retry... Exception: {Exception}",
                            e);
                        _searchTelemetryClient.TrackMetric(SearchTelemetryClient.MetricName.SearchIndexReopenFailed, 1);

                        return true;
                    },
                    maxRetries: maxRetries,
                    waitIncrement: TimeSpan.FromSeconds(1));

                return true;
            }
            catch (Exception e)
            {
                _logger.LogCritical("Startup: A critical error occurred initializing searcher manager. Number of retries exhausted. Exception: {Exception}", e);
                _searchTelemetryClient.TrackMetric(SearchTelemetryClient.MetricName.SearchIndexReopenFailed, maxRetries);

                return false;
            }
        }

        private void TrackIndexMetrics(NuGetSearcherManager searcherManager, SearchTelemetryClient searchTelemetryClient)
        {
            var searcher = searcherManager.Get();
            try
            {
                // Track number of documents in index
                searchTelemetryClient.TrackMetric(SearchTelemetryClient.MetricName.LuceneNumDocs, searcher.IndexReader.NumDocs());

                // Track time between Lucene commit and reopen
                string temp;
                if (searcher.CommitUserData.TryGetValue("commitTimeStamp", out temp))
                {
                    var commitTimestamp = DateTimeOffset.Parse(temp, null, DateTimeStyles.AssumeUniversal);

                    searchTelemetryClient.TrackMetric(SearchTelemetryClient.MetricName.LuceneLoadLag,
                        (searcher.LastReopen - commitTimestamp.UtcDateTime).TotalSeconds,
                        new Dictionary<string, string>()
                        {
                            { SearchTelemetryClient.MetricName.LuceneLastReopen, searcher.LastReopen.ToString("o") },
                            { SearchTelemetryClient.MetricName.LuceneCommitTimestamp, commitTimestamp.UtcDateTime.ToString("o") }
                        });
                }
            }
            finally
            {
                searcherManager.Release(searcher);
            }
        }

        public async Task InvokeAsync(IOwinContext context)
        {
            try
            {
                if (_searcherManager == null)
                {
                    _logger.LogInformation(LogMessages.SearcherManagerNotInitialized);
                    _searchTelemetryClient.TrackMetric(SearchTelemetryClient.MetricName.SearcherManagerNotInitialized, 1);

                    context.Response.StatusCode = (int)HttpStatusCode.ServiceUnavailable;
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
                        case "/query":
                            await ServiceEndpoints.V3SearchAsync(context, _searcherManager, _responseWriter);
                            break;
                        case "/autocomplete":
                            await ServiceEndpoints.AutoCompleteAsync(context, _searcherManager, _responseWriter);
                            break;
                        case "/search/query":
                            await ServiceEndpoints.V2SearchAsync(context, _searcherManager, _responseWriter);
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
                throw;
            }
        }
    }
}
