// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;

namespace NuGet.Services.Logging
{
    public class MachineNameTelemetryInitializer
        : SupportPropertiesTelemetryInitializer
    {
        public MachineNameTelemetryInitializer()
            : base("MachineName", TryGetMachineName())
        {
        }

        private static string TryGetMachineName()
        {
            try
            {
                return Environment.MachineName;
            }
            catch
            {
                return string.Empty;
            }
        }
    }
}
