// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Web;
using Microsoft.ApplicationInsights.DataContracts;
using Microsoft.ApplicationInsights.Extensibility;
using NuGetGallery.Configuration;

namespace NuGetGallery.Diagnostics
{
    public class TelemetryContextInitializer
        : IContextInitializer
    {
        private readonly ConfigurationService _configurationService;

        public TelemetryContextInitializer(ConfigurationService configurationService)
        {
            _configurationService = configurationService;
        }

        public void Initialize(TelemetryContext context)
        {
            if (HttpContext.Current == null)
                return;

            var iKey = _configurationService.Current.AppInsightsInstrumentationKey;
            if (!string.IsNullOrEmpty(iKey))
            {
                context.InstrumentationKey = iKey;
            }
        }
    }
}