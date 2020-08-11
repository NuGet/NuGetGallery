// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;

namespace NuGet.Services.AzureSearch.SearchService
{
    public class ServerStatus
    {
        public string MachineName { get; set; }
        public int ProcessId { get; set; }
        public DateTimeOffset ProcessStartTime { get; set; }
        public TimeSpan ProcessDuration { get; set; }
        public string DeploymentLabel { get; set; }
        public string AssemblyCommitId { get; set; }
        public string AssemblyInformationalVersion { get; set; }
        public string AssemblyBuildDateUtc { get; set; }
        public string InstanceId { get; set; }
        public DateTimeOffset? LastServiceRefreshTime { get; set; }
    }
}
