// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.
using NuGetGallery.Configuration;
using System.Threading.Tasks;


namespace NuGetGallery
{
    public interface IMonitoringService
    {
        Task TriggerAPagerDutyIncident(IAppConfiguration config, string errorMessage);
        Task<string> GetPrimaryOnCall(IAppConfiguration config);
    }
}