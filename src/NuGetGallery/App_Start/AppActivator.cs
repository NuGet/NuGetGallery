﻿// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Security.Claims;
using System.Web.Helpers;
using System.Web.Hosting;
using System.Web.Http;
using System.Web.Http.ExceptionHandling;
using System.Web.Mvc;
using System.Web.Optimization;
using System.Web.Routing;
using System.Web.UI;
using Elmah;
using Microsoft.WindowsAzure.Diagnostics;
using Microsoft.WindowsAzure.ServiceRuntime;
using NuGet.Services.Search.Client.Correlation;
using NuGetGallery;
using NuGetGallery.Configuration;
using NuGetGallery.Diagnostics;
using NuGetGallery.Infrastructure;
using NuGetGallery.Infrastructure.Jobs;
using WebActivatorEx;
using WebBackgrounder;

[assembly: PreApplicationStartMethod(typeof(AppActivator), "PreStart")]
[assembly: PostApplicationStartMethod(typeof(AppActivator), "PostStart")]
[assembly: ApplicationShutdownMethod(typeof(AppActivator), "Stop")]

namespace NuGetGallery
{
    public static class AppActivator
    {
        private static JobManager _jobManager;

        public static void PreStart()
        {
            MessageQueue.Enable(maxPerQueue: 1000);

            AntiForgeryConfig.UniqueClaimTypeIdentifier = ClaimTypes.NameIdentifier;

            ViewEngines.Engines.Clear();
            ViewEngines.Engines.Add(CreateViewEngine());

            try
            {
                if (RoleEnvironment.IsAvailable)
                {
                    CloudPreStart();
                }
            }
            catch
            {
                // Azure SDK not available!
            }
        }

        public static void PostStart()
        {
            if (!OwinStartup.HasRun)
            {
                throw new AppActivatorException("The OwinStartup module did not run. Make sure the application runs in an OWIN pipeline and Microsoft.Owin.Host.SystemWeb.dll is in the bin directory.");
            }

            // Get configuration from the kernel
            var config = DependencyResolver.Current.GetService<IAppConfiguration>();

            BackgroundJobsPostStart(config);
            AppPostStart(config);
            BundlingPostStart();
        }

        public static void Stop()
        {
            BackgroundJobsStop();
        }

        private static RazorViewEngine CreateViewEngine()
        {
            var ret = new RazorViewEngine();

            ret.AreaMasterLocationFormats =
                ret.AreaViewLocationFormats =
                ret.AreaPartialViewLocationFormats =
                new[]
            {
                "~/Areas/{2}/Views/{1}/{0}.cshtml",
                "~/Branding/Views/Shared/{0}.cshtml",
                "~/Areas/{2}/Views/Shared/{0}.cshtml",
            };

            ret.MasterLocationFormats =
                ret.ViewLocationFormats =
                ret.PartialViewLocationFormats =
                new[]
            {
                "~/Branding/Views/{1}/{0}.cshtml",
                "~/Views/{1}/{0}.cshtml",
                "~/Branding/Views/Shared/{0}.cshtml",
                "~/Views/Shared/{0}.cshtml",
                "~/Areas/Admin/Views/DeleteAccount/{0}.cshtml",
            };

            return ret;
        }

        private static void CloudPreStart()
        {
            Trace.Listeners.Add(new DiagnosticMonitorTraceListener());
        }

        private static void BundlingPostStart()
        {
            var jQueryBundle = new ScriptBundle("~/Scripts/jquery")
                .Include("~/Scripts/jquery-{version}.js");
            BundleTable.Bundles.Add(jQueryBundle);

            ScriptManager.ScriptResourceMapping.AddDefinition("jquery",
                new ScriptResourceDefinition
                {
                    Path = jQueryBundle.Path
                });

            var scriptBundle = new ScriptBundle("~/Scripts/all")
                .Include("~/Scripts/jquery-{version}.js")
                .Include("~/Scripts/jquery.validate.js")
                .Include("~/Scripts/jquery.validate.unobtrusive.js")
                .Include("~/Scripts/jquery.timeago.js")
                .Include("~/Scripts/nugetgallery.js");
            BundleTable.Bundles.Add(scriptBundle);

            // Modernizr needs to be delivered at the top of the page but putting it in a bundle gets us a cache-buster.
            // TODO: Use minified modernizr!
            var modernizrBundle = new ScriptBundle("~/Scripts/modernizr")
                .Include("~/Scripts/modernizr-{version}.js");
            BundleTable.Bundles.Add(modernizrBundle);

            Bundle stylesBundle = new StyleBundle("~/Content/css");
            foreach (string filename in new[] {
                    "Site.css",
                    "Layout.css",
                    "PageStylings.css",
                    "fabric.css",
                })
            {
                stylesBundle
                    .Include("~/Content/" + filename)
                    .Include("~/Content/Branding/" + filename)
                    .Include("~/Branding/Content/" + filename);
            }

            BundleTable.Bundles.Add(stylesBundle);

            // Needs a) a separate bundle because of relative pathing in the @font-face directive
            // b) To be a bundle for auto-selection of ".min.css"
            var fontAwesomeBundle = new StyleBundle("~/Content/font-awesome/css");
            fontAwesomeBundle.Include("~/Content/font-awesome/font-awesome.css");
            BundleTable.Bundles.Add(fontAwesomeBundle);

            // Support Requests admin area bundle
            var jQueryUiStylesBundle = new StyleBundle("~/Content/themes/custom/jqueryui")
                .Include("~/Content/themes/custom/jquery-ui-1.10.3.custom.css");
            BundleTable.Bundles.Add(jQueryUiStylesBundle);

            var supportRequestStylesBundle = new StyleBundle("~/Content/supportrequests")
                .Include("~/Content/admin/SupportRequestStyles.css");
            BundleTable.Bundles.Add(supportRequestStylesBundle);

            var supportRequestsBundle = new ScriptBundle("~/Scripts/supportrequests")
                .Include("~/Scripts/jquery-ui-{version}.js")
                .Include("~/Scripts/moment.js")
                .Include("~/Scripts/knockout-2.2.1.js")
                .Include("~/Scripts/knockout.mapping-latest.js")
                .Include("~/Scripts/knockout-projections.js")
                .Include("~/Scripts/supportrequests.js");
            BundleTable.Bundles.Add(supportRequestsBundle);

            // Add bundles for the site redesign
            var newStyleBundle = new StyleBundle("~/Content/gallery/css/site.min.css");
            newStyleBundle
                .Include("~/Content/gallery/css/bootstrap.css")
                .Include("~/Content/gallery/css/bootstrap-theme.css")
                .Include("~/Content/gallery/css/fabric.css");
            BundleTable.Bundles.Add(newStyleBundle);

            var newScriptBundle = new ScriptBundle("~/Scripts/gallery/site.min.js")
                .Include("~/Scripts/gallery/jquery-1.12.4.js")
                .Include("~/Scripts/gallery/jquery.validate-1.16.0.js")
                .Include("~/Scripts/gallery/jquery.validate.unobtrusive-3.2.6.js")
                .Include("~/Scripts/gallery/knockout-3.4.2.js")
                .Include("~/Scripts/gallery/bootstrap.js")
                .Include("~/Scripts/gallery/moment-2.18.1.js")
                .Include("~/Scripts/gallery/common.js")
                .Include("~/Scripts/gallery/autocomplete.js");
            BundleTable.Bundles.Add(newScriptBundle);

            var d3ScriptBundle = new ScriptBundle("~/Scripts/gallery/stats.min.js")
                .Include("~/Scripts/gallery/d3.v3.js")
                .Include("~/Scripts/gallery/stats-perpackagestatsgraphs.js")
                .Include("~/Scripts/gallery/stats-dimensions.js");
            BundleTable.Bundles.Add(d3ScriptBundle);

            var homeScriptBundle = new ScriptBundle("~/Scripts/gallery/page-home.min.js")
                .Include("~/Scripts/gallery/page-home.js");
            BundleTable.Bundles.Add(homeScriptBundle);

            var signinScriptBundle = new ScriptBundle("~/Scripts/gallery/page-signin.min.js")
                .Include("~/Scripts/gallery/page-signin.js");
            BundleTable.Bundles.Add(signinScriptBundle);

            var displayPackageScriptBundle = new ScriptBundle("~/Scripts/gallery/page-display-package.min.js")
                .Include("~/Scripts/gallery/page-display-package.js")
                .Include("~/Scripts/gallery/clamp.js");
            BundleTable.Bundles.Add(displayPackageScriptBundle);

            var managePackagesScriptBundle = new ScriptBundle("~/Scripts/gallery/page-manage-packages.min.js")
                .Include("~/Scripts/gallery/page-manage-packages.js");
            BundleTable.Bundles.Add(managePackagesScriptBundle);

            var manageOwnersScriptBundle = new ScriptBundle("~/Scripts/gallery/page-manage-owners.min.js")
                .Include("~/Scripts/gallery/page-manage-owners.js");
            BundleTable.Bundles.Add(manageOwnersScriptBundle);

            var aboutScriptBundle = new ScriptBundle("~/Scripts/gallery/page-about.min.js")
                .Include("~/Scripts/gallery/page-about.js");
            BundleTable.Bundles.Add(aboutScriptBundle);

            var apiKeysScriptBundle = new ScriptBundle("~/Scripts/gallery/page-api-keys.min.js")
                .Include("~/Scripts/gallery/page-api-keys.js");
            BundleTable.Bundles.Add(apiKeysScriptBundle);

            var accountScriptBundle = new ScriptBundle("~/Scripts/gallery/page-account.min.js")
                .Include("~/Scripts/gallery/page-account.js");
            BundleTable.Bundles.Add(accountScriptBundle);

            var manageOrganizationScriptBundle = new ScriptBundle("~/Scripts/gallery/page-manage-organization.min.js")
                .Include("~/Scripts/gallery/page-manage-organization.js");
            BundleTable.Bundles.Add(manageOrganizationScriptBundle);

            var addOrganizationScriptBundle = new ScriptBundle("~/Scripts/gallery/page-add-organization.min.js")
                .Include("~/Scripts/gallery/page-add-organization.js")
                .Include("~/Scripts/gallery/md5.js");
            BundleTable.Bundles.Add(addOrganizationScriptBundle);
        }

        private static void AppPostStart(IAppConfiguration configuration)
        {
            WebApiConfig.Register(GlobalConfiguration.Configuration);
            NuGetODataConfig.Register(GlobalConfiguration.Configuration);

            // Attach correlator
            GlobalConfiguration.Configuration.MessageHandlers.Add(new WebApiCorrelationHandler());

            // Log unhandled exceptions
            GlobalConfiguration.Configuration.Services.Add(typeof(IExceptionLogger), new QuietExceptionLogger());

            Routes.RegisterRoutes(RouteTable.Routes, configuration.FeedOnlyMode);
            AreaRegistration.RegisterAllAreas();

            GlobalFilters.Filters.Add(new SendErrorsToTelemetryAttribute { View = "~/Views/Errors/InternalError.cshtml" });
            GlobalFilters.Filters.Add(new ReadOnlyModeErrorFilter());
            GlobalFilters.Filters.Add(new AntiForgeryErrorFilter());
            ValueProviderFactories.Factories.Add(new HttpHeaderValueProviderFactory());
        }

        private static void BackgroundJobsPostStart(IAppConfiguration configuration)
        {
            var indexer = DependencyResolver.Current.GetService<IIndexingService>();
            var jobs = new List<IJob>();
            if (indexer != null)
            {
                indexer.RegisterBackgroundJobs(jobs, configuration);
            }

            if (configuration.CollectPerfLogs)
            {
                jobs.Add(CreateLogFlushJob());
            }

            if (configuration.StorageType == StorageType.AzureStorage)
            {
                var cloudDownloadCountService = DependencyResolver.Current.GetService<IDownloadCountService>() as CloudDownloadCountService;
                if (cloudDownloadCountService != null)
                {
                    // Perform initial refresh + schedule new refreshes every 15 minutes
                    HostingEnvironment.QueueBackgroundWorkItem(cancellationToken => cloudDownloadCountService.Refresh());
                    jobs.Add(new CloudDownloadCountServiceRefreshJob(TimeSpan.FromMinutes(15), cloudDownloadCountService));
                }
            }

            if (jobs.AnySafe())
            {
                var jobCoordinator = new NuGetJobCoordinator();
                _jobManager = new JobManager(jobs, jobCoordinator)
                {
                    RestartSchedulerOnFailure = true
                };
                _jobManager.Fail(e => ErrorLog.GetDefault(null).Log(new Error(e)));
                _jobManager.Start();
            }
        }

        private static ProcessPerfEvents CreateLogFlushJob()
        {
            var logDirectory = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "App_Data", "Logs");
            try
            {
                if (RoleEnvironment.IsAvailable)
                {
                    var resource = RoleEnvironment.GetLocalResource("Logs");
                    if (resource != null)
                    {
                        logDirectory = Path.Combine(resource.RootPath);
                    }
                }
            }
            catch
            {
                // Meh, so Azure isn't available...
            }
            return new ProcessPerfEvents(
                TimeSpan.FromSeconds(10),
                logDirectory,
                new[] { "ExternalSearchService" },
                timeout: TimeSpan.FromSeconds(10));
        }

        private static void BackgroundJobsStop()
        {
            if (_jobManager != null)
            {
                _jobManager.Dispose();
            }
        }
    }
}
