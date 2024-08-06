// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;
using Microsoft.VisualStudio.Setup.Configuration;

namespace NuGet.Services.Build
{
    public static class VisualStudioSetupConfigurationHelper
    {
        public static string[] GetInstancePaths()
        {
            var instancePaths = new List<string>();

            var query = new SetupConfiguration();
            var e = query.EnumAllInstances();
            int fetched;

            var instances = new ISetupInstance[1];
            do
            {
                e.Next(1, instances, out fetched);
                if (fetched > 0)
                {
                    instancePaths.Add(instances[0].GetInstallationPath());
                }
            } while (fetched > 0);

            return instancePaths.ToArray();
        }
    }
}
