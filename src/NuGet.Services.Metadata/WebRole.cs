// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.
using System.Linq;
using Microsoft.WindowsAzure.ServiceRuntime;

namespace NuGet.Services.Metadata
{
    public class WebRole : RoleEntryPoint
    {
        public override bool OnStart()
        {
            RoleEnvironment.Changing += RoleEnvironmentOnChanging;

            return base.OnStart();
        }

        private void RoleEnvironmentOnChanging(object sender, RoleEnvironmentChangingEventArgs eventArgs)
        {
            // If a configuration setting is changing 
            if (eventArgs.Changes.Any(change => change is RoleEnvironmentConfigurationSettingChange))
            {
                // Set e.Cancel to true to restart this role instance 
                eventArgs.Cancel = true;
            }
        }
    }
}