﻿// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using System.Web;
using System.Web.Http;
using System.Web.Mvc;
using Elmah;
using Microsoft.ApplicationInsights;
using Microsoft.ApplicationInsights.Extensibility;
using Microsoft.Owin;
using Microsoft.Owin.Logging;
using Microsoft.Owin.Security;
using Microsoft.Owin.Security.Cookies;
using NuGet.Services.Logging;
using NuGetGallery.Authentication;
using NuGetGallery.Authentication.Providers;
using NuGetGallery.Authentication.Providers.Cookie;
using NuGetGallery.Configuration;
using NuGetGallery.Infrastructure;
using Owin;

[assembly: OwinStartup(typeof(NuGetGallery.OwinStartup))]

namespace NuGetGallery
{
    public class OwinStartup
    {
        public static bool HasRun { get; private set; }

        // This method is auto-detected by the OWIN pipeline. DO NOT RENAME IT!
        public static void Configuration(IAppBuilder app)
        {
            // Tune ServicePointManager
            // (based on http://social.technet.microsoft.com/Forums/en-US/windowsazuredata/thread/d84ba34b-b0e0-4961-a167-bbe7618beb83 and https://msdn.microsoft.com/en-us/library/system.net.servicepointmanager.aspx)
            ServicePointManager.DefaultConnectionLimit = 500;
            ServicePointManager.UseNagleAlgorithm = false;
            ServicePointManager.Expect100Continue = false;

            // Ensure that SSLv3 is disabled and that Tls v1.2 is enabled.
            ServicePointManager.SecurityProtocol &= ~SecurityProtocolType.Ssl3;
            ServicePointManager.SecurityProtocol |= SecurityProtocolType.Tls12;

            // Setting time out for all RegEx objects. Noted in remarks at https://msdn.microsoft.com/en-us/library/system.text.regularexpressions.regex.matchtimeout%28v=vs.110%29.aspx
            AppDomain.CurrentDomain.SetData("REGEX_DEFAULT_MATCH_TIMEOUT", TimeSpan.FromSeconds(10));

            // Register IoC
            app.UseAutofacInjection(GlobalConfiguration.Configuration);
            var dependencyResolver = DependencyResolver.Current;

            // Register Elmah
            var elmahServiceCenter = new DependencyResolverServiceProviderAdapter(dependencyResolver);
            ServiceCenter.Current = _ => elmahServiceCenter;

            // Get config
            var config = dependencyResolver.GetService<IGalleryConfigurationService>();
            var auth = dependencyResolver.GetService<AuthenticationService>();

            // Setup telemetry
            var instrumentationKey = config.Current.AppInsightsInstrumentationKey;
            if (!string.IsNullOrEmpty(instrumentationKey))
            {
                TelemetryConfiguration.Active.InstrumentationKey = instrumentationKey;

                // Add enrichers
                TelemetryConfiguration.Active.TelemetryInitializers.Add(new ClientInformationTelemetryEnricher());

                var telemetryProcessorChainBuilder = TelemetryConfiguration.Active.TelemetryProcessorChainBuilder;

                // Add processors
                telemetryProcessorChainBuilder.Use(next =>
                {
                    var processor = new RequestTelemetryProcessor(next);

                    processor.SuccessfulResponseCodes.Add(400);
                    processor.SuccessfulResponseCodes.Add(404);

                    return processor;
                });

                telemetryProcessorChainBuilder.Use(next => new ClientTelemetryPIIProcessor(next));

                var telemetry = dependencyResolver.GetService<TelemetryClientWrapper>();
                telemetryProcessorChainBuilder.Use(
                    next => new ExceptionTelemetryProcessor(next, telemetry.UnderlyingClient));

                // Note: sampling rate must be a factor 100/N where N is a whole number
                // e.g.: 50 (= 100/2), 33.33 (= 100/3), 25 (= 100/4), ...
                // https://azure.microsoft.com/en-us/documentation/articles/app-insights-sampling/
                var instrumentationSamplingPercentage = config.Current.AppInsightsSamplingPercentage;
                if (instrumentationSamplingPercentage > 0 && instrumentationSamplingPercentage < 100)
                {
                    telemetryProcessorChainBuilder.UseSampling(instrumentationSamplingPercentage);
                }

                telemetryProcessorChainBuilder.Build();
            }

            // Configure logging
            app.SetLoggerFactory(new DiagnosticsLoggerFactory());

            // Remove X-AspNetMvc-Version header
            MvcHandler.DisableMvcResponseHeader = true;

            if (config.Current.RequireSSL)
            {
                // Put a middleware at the top of the stack to force the user over to SSL
                if (config.Current.ForceSslExclusion == null)
                {
                    app.UseForceSsl(config.Current.SSLPort);
                }
                else
                {
                    app.UseForceSsl(config.Current.SSLPort, config.Current.ForceSslExclusion);
                }
            }

            // Get the local user auth provider, if present and attach it first
            Authenticator localUserAuthenticator;
            if (auth.Authenticators.TryGetValue(Authenticator.GetName(typeof(LocalUserAuthenticator)), out localUserAuthenticator))
            {
                // Configure cookie auth now
                localUserAuthenticator.Startup(config, app).Wait();
            }

            // Attach external sign-in cookie middleware
            app.SetDefaultSignInAsAuthenticationType(AuthenticationTypes.External);
            app.UseCookieAuthentication(new CookieAuthenticationOptions
            {
                AuthenticationType = AuthenticationTypes.External,
                AuthenticationMode = AuthenticationMode.Passive,
                CookieName = ".AspNet." + AuthenticationTypes.External,
                ExpireTimeSpan = TimeSpan.FromMinutes(5)
            });

            // Attach non-cookie auth providers
            var nonCookieAuthers = auth
                .Authenticators
                .Where(p => !String.Equals(
                    p.Key,
                    Authenticator.GetName(typeof(LocalUserAuthenticator)),
                    StringComparison.OrdinalIgnoreCase))
                .Select(p => p.Value);
            foreach (var auther in nonCookieAuthers)
            {
                auther.Startup(config, app).Wait();
            }

            // Catch unobserved exceptions from threads before they cause IIS to crash:
            TaskScheduler.UnobservedTaskException += (sender, exArgs) =>
            {
                // Send to AppInsights
                try
                {
                    var telemetryClient = new TelemetryClient();
                    telemetryClient.TrackException(exArgs.Exception, new Dictionary<string, string>()
                    {
                        {"ExceptionOrigin", "UnobservedTaskException"}
                    });
                }
                catch (Exception)
                {
                    // this is a tragic moment... swallow Exception to prevent crashing IIS
                }

                // Send to ELMAH
                try
                {
                    HttpContext current = HttpContext.Current;
                    if (current != null)
                    {
                        var errorSignal = ErrorSignal.FromContext(current);
                        if (errorSignal != null)
                        {
                            errorSignal.Raise(exArgs.Exception, current);
                        }
                    }
                }
                catch (Exception)
                {
                    // more tragedy... swallow Exception to prevent crashing IIS
                }

                exArgs.SetObserved();
            };

            HasRun = true;
        }
    }
}