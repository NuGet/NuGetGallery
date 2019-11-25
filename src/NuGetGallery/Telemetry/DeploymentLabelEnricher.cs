// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using Microsoft.ApplicationInsights.Channel;
using Microsoft.ApplicationInsights.DataContracts;
using Microsoft.ApplicationInsights.Extensibility;
using NuGetGallery.Configuration;

namespace NuGetGallery
{
    public class DeploymentLabelEnricher : ITelemetryInitializer
    {
        private const string DeploymentLabel = "DeploymentLabel";
        private readonly string _deploymentLabel;

        public DeploymentLabelEnricher(IAppConfiguration appConfiguration)
        {
            if (appConfiguration == null)
            {
                throw new ArgumentNullException(nameof(appConfiguration));
            }
            
            _deploymentLabel = appConfiguration.DeploymentLabel;
        }

        public void Initialize(ITelemetry telemetry)
        {
            if (_deploymentLabel == null)
            {
                return;
            }

            if (!(telemetry is ISupportProperties itemTelemetry))
            {
                return;
            }

            if (itemTelemetry.Properties.ContainsKey(DeploymentLabel))
            {
                return;
            }

            itemTelemetry.Properties.Add(DeploymentLabel, _deploymentLabel);
        }
    }
}