// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Security.Claims;
using System.Web.Helpers;
using System.Web.Hosting;
using System.Web.Http;
using System.Web.Http.ExceptionHandling;
using System.Web.Mvc;
using System.Web.Optimization;
using System.Web.Routing;
using System.Web.UI;
using Microsoft.Extensions.DependencyInjection;
using NuGetGallery;
using NuGetGallery.Configuration;
using NuGetGallery.Diagnostics;
using NuGetGallery.Infrastructure;
using NuGetGallery.Infrastructure.Jobs;
using NuGetGallery.Infrastructure.Lucene;
using NuGetGallery.Infrastructure.Search.Correlation;
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
            Trace.AutoFlush = true;

            MessageQueue.Enable(maxPerQueue: 1000);

            AntiForgeryConfig.UniqueClaimTypeIdentifier = ClaimTypes.NameIdentifier;

            ViewEngines.Engines.Clear();
            ViewEngines.Engines.Add(CreateViewEngine());
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

        private static void BundlingPostStart()
        {
            // Add primary style bundle
            Bundle stylesBundle = new StyleBundle("~/Content/css.min.css");
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

            // Bootstrap is no longer bundled in site.min.css given that the package that does the minification
            // cannot understand new CSS feature, instead we are using Grunt to create a bootstrap.min.css file
            // for all bootstrap styles.
            var newStyleBundle = new StyleBundle("~/Content/gallery/css/site.min.css");
            newStyleBundle
                .Include("~/Content/gallery/css/fabric.css");
            BundleTable.Bundles.Add(newStyleBundle);

            // Add scripts bundles
            var instrumentationBundle = new ScriptBundle("~/Scripts/gallery/instrumentation.min.js")
                .Include("~/Scripts/gallery/instrumentation.js");
            BundleTable.Bundles.Add(instrumentationBundle);

            var scriptBundle = new ScriptBundle("~/Scripts/gallery/site.min.js")
                .Include("~/Scripts/gallery/jquery-3.4.1.js")
                .Include("~/Scripts/gallery/jquery.validate-1.16.0.js")
                .Include("~/Scripts/gallery/jquery.validate.unobtrusive-3.2.6.js")
                .Include("~/Scripts/gallery/knockout-3.5.1.js")
                .Include("~/Scripts/gallery/bootstrap.js")
                .Include("~/Scripts/gallery/moment-2.29.4.js")
                .Include("~/Scripts/gallery/common.js")
                .Include("~/Scripts/gallery/autocomplete.js");
            BundleTable.Bundles.Add(scriptBundle);

            var d3ScriptBundle = new ScriptBundle("~/Scripts/gallery/stats.min.js")
                .Include("~/Scripts/d3/d3.js")
                .Include("~/Scripts/gallery/stats-perpackagestatsgraphs.js")
                .Include("~/Scripts/gallery/stats-dimensions.js");
            BundleTable.Bundles.Add(d3ScriptBundle);

            var multiSelectDropdownBundle = new ScriptBundle("~/Scripts/gallery/common-multi-select-dropdown.min.js")
                .Include("~/Scripts/gallery/common-multi-select-dropdown.js");
            BundleTable.Bundles.Add(multiSelectDropdownBundle);

            var asyncFileUploadScriptBundle = new ScriptBundle("~/Scripts/gallery/async-file-upload.min.js")
                .Include("~/Scripts/gallery/async-file-upload.js");
            BundleTable.Bundles.Add(asyncFileUploadScriptBundle);

            var certificatesScriptBundle = new ScriptBundle("~/Scripts/gallery/certificates.min.js")
                .Include("~/Scripts/gallery/certificates.js");
            BundleTable.Bundles.Add(certificatesScriptBundle);

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

            var listPackagesScriptBundle = new ScriptBundle("~/Scripts/gallery/page-list-packages.min.js")
                .Include("~/Scripts/gallery/page-list-packages.js");
            BundleTable.Bundles.Add(listPackagesScriptBundle);

            var managePackagesScriptBundle = new ScriptBundle("~/Scripts/gallery/page-manage-packages.min.js")
                .Include("~/Scripts/gallery/page-manage-packages.js");
            BundleTable.Bundles.Add(managePackagesScriptBundle);

            var manageOwnersScriptBundle = new ScriptBundle("~/Scripts/gallery/page-manage-owners.min.js")
                .Include("~/Scripts/gallery/page-manage-owners.js");
            BundleTable.Bundles.Add(manageOwnersScriptBundle);

            var manageDeprecationScriptBundle = new ScriptBundle("~/Scripts/gallery/page-manage-deprecation.min.js")
                .Include("~/Scripts/gallery/page-manage-deprecation.js");
            BundleTable.Bundles.Add(manageDeprecationScriptBundle);

            var deletePackageScriptBundle = new ScriptBundle("~/Scripts/gallery/page-delete-package.min.js")
                .Include("~/Scripts/gallery/page-delete-package.js");
            BundleTable.Bundles.Add(deletePackageScriptBundle);

            var editReadMeScriptBundle = new ScriptBundle("~/Scripts/gallery/page-edit-readme.min.js")
                .Include("~/Scripts/gallery/page-edit-readme.js");
            BundleTable.Bundles.Add(editReadMeScriptBundle);

            var aboutScriptBundle = new ScriptBundle("~/Scripts/gallery/page-about.min.js")
                .Include("~/Scripts/gallery/page-about.js");
            BundleTable.Bundles.Add(aboutScriptBundle);

            var downloadsScriptBundle = new ScriptBundle("~/Scripts/gallery/page-downloads.min.js")
                .Include("~/Scripts/gallery/page-downloads.js");
            BundleTable.Bundles.Add(downloadsScriptBundle);

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

            var syntaxhighlightScriptBundle = new ScriptBundle("~/Scripts/gallery/syntaxhighlight.min.js")
                .Include("~/Scripts/gallery/syntaxhighlight.js");
            BundleTable.Bundles.Add(syntaxhighlightScriptBundle);

            // This is needed for the Admin database viewer.
            ScriptManager.ScriptResourceMapping.AddDefinition("jquery",
                new ScriptResourceDefinition { Path = scriptBundle.Path });

            // Add support requests bundles
            var supportRequestStylesBundle = new StyleBundle("~/Content/themes/custom/page-support-requests.min.css")
                .Include("~/Content/themes/custom/jquery-ui-1.10.3.custom.css")
                .Include("~/Content/admin/SupportRequestStyles.css");
            BundleTable.Bundles.Add(supportRequestStylesBundle);

            var supportRequestsBundle = new ScriptBundle("~/Scripts/page-support-requests.min.js")
                .Include("~/Scripts/gallery/jquery-ui-1.10.3.js")
                .Include("~/Scripts/gallery/knockout-projections.js")
                .Include("~/Scripts/gallery/page-support-requests.js");
            BundleTable.Bundles.Add(supportRequestsBundle);
        }

        private static void AppPostStart(IAppConfiguration configuration)
        {
            WebApiConfig.Register(GlobalConfiguration.Configuration);
            NuGetODataConfig.Register(GlobalConfiguration.Configuration);

            // Attach correlator
            GlobalConfiguration.Configuration.MessageHandlers.Add(new WebApiCorrelationHandler());

            // Log unhandled exceptions
            GlobalConfiguration.Configuration.Services.Add(typeof(IExceptionLogger), new QuietExceptionLogger());

            Routes.RegisterRoutes(RouteTable.Routes, configuration.FeedOnlyMode, configuration.AdminPanelEnabled);
            AreaRegistration.RegisterAllAreas();

            GlobalFilters.Filters.Add(new SendErrorsToTelemetryAttribute { View = "~/Views/Errors/InternalError.cshtml" });
            GlobalFilters.Filters.Add(new ReadOnlyModeErrorFilter());
            GlobalFilters.Filters.Add(new AntiForgeryErrorFilter());
            GlobalFilters.Filters.Add(new UserDeletedErrorFilter());
            GlobalFilters.Filters.Add(new RequestValidationExceptionFilter());
            ValueProviderFactories.Factories.Add(new HttpHeaderValueProviderFactory());
        }

        private static void BackgroundJobsPostStart(IAppConfiguration configuration)
        {
            var indexingJobFactory = DependencyResolver.Current.GetService<IIndexingJobFactory>();
            var jobs = new List<IJob>();
            if (indexingJobFactory != null)
            {
                indexingJobFactory.RegisterBackgroundJobs(jobs, configuration);
            }

            if (configuration.StorageType == StorageType.AzureStorage)
            {
                var cloudDownloadCountService = DependencyResolver.Current.GetService<IDownloadCountService>() as CloudDownloadCountService;
                if (cloudDownloadCountService != null)
                {
                    // Perform initial refresh + schedule new refreshes every 15 minutes
                    HostingEnvironment.QueueBackgroundWorkItem(_ => cloudDownloadCountService.RefreshAsync());
                    jobs.Add(new CloudDownloadCountServiceRefreshJob(TimeSpan.FromMinutes(15),
                        cloudDownloadCountService));
                }
            }

            // Perform initial refresh for vulnerabilities cache + schedule new refreshes every 30 minutes
            var packageVulnerabilitiesCacheService = DependencyResolver.Current.GetService<IPackageVulnerabilitiesCacheService>();
            var serviceScopeFactory = DependencyResolver.Current.GetService<IServiceScopeFactory>();
            HostingEnvironment.QueueBackgroundWorkItem(_ => packageVulnerabilitiesCacheService.RefreshCache(serviceScopeFactory));
            jobs.Add(new PackageVulnerabilitiesCacheRefreshJob(TimeSpan.FromMinutes(30), packageVulnerabilitiesCacheService, serviceScopeFactory));

            if (jobs.AnySafe())
            {
                var jobCoordinator = new NuGetJobCoordinator();
                _jobManager = new JobManager(jobs, jobCoordinator)
                {
                    RestartSchedulerOnFailure = true
                };
                _jobManager.Fail(e => { Trace.TraceError($"{nameof(BackgroundJobsPostStart)} failure: {e.Message}"); });
                _jobManager.Start();
            }
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
